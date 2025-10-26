using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat.Permissions;
using Everywhere.Chat.Plugins;
using Everywhere.Interop;
using Everywhere.Utilities;
using MessagePack;
using ObservableCollections;

namespace Everywhere.Chat;

public delegate void ChatContextChangedEventHandler(ChatContext context);

/// <summary>
/// Maintains the context of the chat, including a tree of <see cref="ChatMessageNode"/> and other metadata.
/// The current branch is derived by following each node's <see cref="ChatMessageNode.ChoiceIndex"/>.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public partial class ChatContext : ObservableObject, IReadOnlyList<ChatMessageNode>
{
    [IgnoreMember]
    public bool IsTemporary
    {
        get;
        set
        {
            // Cannot change IsTemporary if there are messages in the context.
            if (_messageNodeMap.Count == 0) field = value;
            else field = false;
            OnPropertyChanged();
        }
    }

    [Key(0)]
    public ChatContextMetadata Metadata { get; }

    /// <summary>
    /// Messages in the current branch.
    /// </summary>
    [IgnoreMember]
    public int Count => _branchNodes.Count;

    [IgnoreMember]
    public string SystemPrompt
    {
        get => _rootNode.Message.To<SystemChatMessage>().SystemPrompt;
        set => _rootNode.Message.To<SystemChatMessage>().SystemPrompt = value;
    }

    /// <summary>
    /// Key: VisualElement.Id
    /// Value: VisualElement.
    /// VisualElement is dynamically created and not serialized, so we keep a map here to track them.
    /// This is also not serialized.
    /// </summary>
    [IgnoreMember]
    public ResilientCache<int, IVisualElement> VisualElements { get; } = new();

    /// <summary>
    /// A map of granted permissions for plugin functions in this chat context (session).
    /// Key: PluginName.FunctionName.id
    /// Value: Granted permissions for the function.
    /// </summary>
    [IgnoreMember]
    public Dictionary<string, ChatFunctionPermissions> GrantedPermissions { get; } = new();

    public ChatMessageNode this[int index] => _branchNodes[index];

    /// <summary>
    /// Event raised when the chat context is modified, e.g., a new message is added or a node is updated.
    /// </summary>
    public event ChatContextChangedEventHandler? Changed;

    /// <summary>
    /// Backing store for MessagePack (de)serialization: nodes are persisted as a collection, and linked by Ids.
    /// </summary>
    [Key(1)]
    private ICollection<ChatMessageNode> MessageNodes => _messageNodeMap.Values;

    /// <summary>
    /// Root node (Guid.Empty) containing the System Prompt.
    /// </summary>
    [Key(2)]
    private readonly ChatMessageNode _rootNode;

    /// <summary>
    /// Map of all message nodes by their ID. This allows for quick access to any node in the context.
    /// NOTE that this map does not include the root node, which is always at Id = Guid.Empty.
    /// </summary>
    [IgnoreMember] private readonly Dictionary<Guid, ChatMessageNode> _messageNodeMap = new();

    /// <summary>
    /// Nodes on the currently selected branch. [0] is always the root node.
    /// </summary>
    [IgnoreMember] private readonly ObservableList<ChatMessageNode> _branchNodes = [];

    /// <summary>
    /// Constructor for MessagePack deserialization and for creating a new chat context with existing nodes.
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="messageNodes"></param>
    /// <param name="rootNode"></param>
    public ChatContext(ChatContextMetadata metadata, ICollection<ChatMessageNode> messageNodes, ChatMessageNode rootNode)
    {
        Metadata = metadata;
        _messageNodeMap.AddRange(messageNodes.Select(v => new KeyValuePair<Guid, ChatMessageNode>(v.Id, v)));
        _rootNode = rootNode;
        _branchNodes.Add(rootNode);

        foreach (var node in messageNodes.Append(rootNode))
        {
            node.Context = this;
            node.PropertyChanged += HandleNodePropertyChanged;
            foreach (var childId in node.Children) _messageNodeMap[childId].Parent = node;
        }

        if (_messageNodeMap.ContainsKey(Guid.Empty))
            throw new InvalidOperationException("Root node (Guid.Empty) must not be in the messageNodeMap.");

        UpdateBranchAfter(0, rootNode);

        metadata.PropertyChanged += HandleMetadataPropertyChanged;
    }

    /// <summary>
    /// Creates a new chat context with the given system prompt. A new Guid v7 ID is assigned.
    /// </summary>
    public ChatContext(string systemPrompt)
    {
        Metadata = new ChatContextMetadata
        {
            Id = Guid.CreateVersion7(),
            DateCreated = DateTimeOffset.UtcNow,
            DateModified = DateTimeOffset.UtcNow,
        };
        _rootNode = ChatMessageNode.CreateRootNode(systemPrompt);
        _rootNode.PropertyChanged += HandleNodePropertyChanged;
        _branchNodes.Add(_rootNode);

        Metadata.PropertyChanged += HandleMetadataPropertyChanged;
    }

    public NotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ToNotifyCollectionChanged(
        Action<ISynchronizedView<ChatMessageNode, ChatMessageNode>>? transform,
        ICollectionEventDispatcher? collectionEventDispatcher) =>
        transform is not null ?
            _branchNodes.CreateView(x => x).With(transform).ToNotifyCollectionChanged(collectionEventDispatcher) :
            _branchNodes.ToWritableNotifyCollectionChanged(collectionEventDispatcher);

    /// <summary>
    /// Create a new branch on the specified sibling node by inserting a new message at that position.
    /// </summary>
    public void CreateBranchOn(ChatMessageNode siblingNode, ChatMessage chatMessage)
    {
        var index = _branchNodes.IndexOf(siblingNode);
        var afterNode = index switch
        {
            < 0 => throw new ArgumentException("The specified node is not in the current branch.", nameof(siblingNode)),
            0 => _rootNode,
            _ => _branchNodes[index - 1]
        };

        var newNode = new ChatMessageNode(chatMessage)
        {
            Context = this,
            Parent = afterNode,
        };
        newNode.PropertyChanged += HandleNodePropertyChanged;
        _messageNodeMap[newNode.Id] = newNode;

        afterNode.Children.Add(newNode.Id);
        afterNode.ChoiceIndex = afterNode.Children.Count - 1;

        UpdateBranchAfter(index - 1, afterNode);
    }

    public void Insert(int index, ChatMessage chatMessage) => Insert(index, new ChatMessageNode(chatMessage) { Context = this });

    /// <summary>
    /// Adds a message at the end of the current branch.
    /// </summary>
    public void Add(ChatMessage message)
    {
        Insert(_branchNodes.Count, new ChatMessageNode(message) { Context = this });
    }

    /// <summary>
    /// Gets all nodes in the chat context in all branches, including the root node.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ChatMessageNode> GetAllNodes()
    {
        yield return _rootNode;
        foreach (var node in _messageNodeMap.Values)
        {
            yield return node;
        }
    }

    public IEnumerator<ChatMessageNode> GetEnumerator() => _branchNodes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_branchNodes).GetEnumerator();

    /// <summary>
    /// Handles changes to the metadata properties.
    /// When any property changes, the Changed event is raised to notify subscribers.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleMetadataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Changed?.Invoke(this);
    }

    private void HandleNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Metadata.DateModified = DateTimeOffset.UtcNow;
        if (e.PropertyName != nameof(ChatMessageNode.ChoiceIndex)) return;
        UpdateBranchAfterNode(sender.NotNull<ChatMessageNode>());
    }

    /// <summary>
    /// Rebuilds the current branch from the specified node forward.
    /// </summary>
    private void UpdateBranchAfterNode(ChatMessageNode node) => UpdateBranchAfter(_branchNodes.IndexOf(node), node);

    private void UpdateBranchAfter(int index, ChatMessageNode node)
    {
        if (index == -1)
            throw new ArgumentOutOfRangeException(nameof(index), "Node is not in the branch nodes.");

        for (var i = _branchNodes.Count - 1; i > index; i--) _branchNodes.RemoveAt(i);

        // Follow ChoiceIndex down the tree.
        while (true)
        {
            if (node.ChoiceIndex < 0 || node.ChoiceIndex >= node.Children.Count) break;
            _branchNodes.Add(node = _messageNodeMap[node.Children[node.ChoiceIndex]]);
        }
    }

    private void Insert(int index, ChatMessageNode newNode)
    {
        if (newNode.Id == Guid.Empty)
            throw new ArgumentException("New node must have a non-empty ID.", nameof(newNode));

        _messageNodeMap[newNode.Id] = newNode;
        newNode.PropertyChanged += HandleNodePropertyChanged;

        var afterNode = index switch
        {
            0 => _rootNode,
            _ => _branchNodes[index - 1]
        };

        if (afterNode.Children.Count > 0)
        {
            newNode.Children.AddRange(afterNode.Children);
            newNode.ChoiceIndex = afterNode.ChoiceIndex;
            foreach (var afterNodeChildId in afterNode.Children)
            {
                _messageNodeMap[afterNodeChildId].Parent = newNode;
            }

            afterNode.Children.Clear();
        }

        newNode.Parent = afterNode;
        afterNode.Children.Add(newNode.Id);

        UpdateBranchAfter(index - 1, afterNode);
    }
}

/// <summary>Chat context metadata persisted along with the object graph.</summary>
[MessagePackObject(AllowPrivate = true)]
public partial class ChatContextMetadata : ObservableObject
{
    /// <summary>
    /// Stable ID (Guid v7) to align with database primary key.
    /// </summary>
    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public DateTimeOffset DateCreated { get; set; }

    [Key(2)]
    [ObservableProperty]
    public partial DateTimeOffset DateModified { get; set; }

    [Key(3)]
    [ObservableProperty]
    public partial string? Topic { get; set; }
}

/// <summary>Tree node in the chat history. The current branch is resolved by ChoiceIndex per node.</summary>
[MessagePackObject(AllowPrivate = true)]
public partial class ChatMessageNode : ObservableObject
{
    [Key(0)]
    public Guid Id { get; }

    [Key(1)]
    public ChatMessage Message { get; }

    [Key(2)]
    public ObservableList<Guid> Children { get; }

    /// <summary>
    /// Index of the chosen child in <see cref="Children"/> (-1 when none).
    /// When persisted, it should be mapped to the child's ID (ChoiceChildId) to avoid index drift under concurrent inserts.
    /// </summary>
    [Key(3)]
    public int ChoiceIndex
    {
        get => Math.Min(field, Children.Count - 1);
        set => SetProperty(ref field, Math.Clamp(value, -1, Children.Count - 1));
    }

    [IgnoreMember]
    public int ChoiceCount => Children.Count;

    [IgnoreMember]
    public ChatMessageNode? Parent { get; internal set; }

    [IgnoreMember]
    [field: AllowNull, MaybeNull]
    public ChatContext Context
    {
        get => field ?? throw new InvalidOperationException("This node is not attached to a ChatContext.");
        internal set;
    }

    /// <summary>
    /// Constructor for MessagePack deserialization and for creating new nodes with existing children.
    /// </summary>
    public ChatMessageNode(Guid id, ChatMessage message, ObservableList<Guid> children)
    {
        Id = id;
        Message = message ?? throw new ArgumentNullException(nameof(message)); // messagepack may pass null here so we guard against it
        message.PropertyChanged += OnMessagePropertyChanged;
        Children = children ?? throw new ArgumentNullException(nameof(children));
        children.CollectionChanged += OnChildrenChanged;
    }

    public ChatMessageNode(ChatMessage message) : this(Guid.CreateVersion7(), message, []) { }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Message));

    private void OnChildrenChanged(in NotifyCollectionChangedEventArgs<Guid> e) => OnPropertyChanged(nameof(ChoiceCount));

    internal static ChatMessageNode CreateRootNode(string systemPrompt) => new(Guid.Empty, new SystemChatMessage(systemPrompt), []);
}