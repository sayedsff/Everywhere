using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePack;
using ObservableCollections;

namespace Everywhere.Models;

/// <summary>
/// Maintains the context of the chat, including the list of <see cref="ChatMessage"/> and other relevant information.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public partial class ChatContext : ObservableObject, IEnumerable<ChatMessageNode>
{
    [Key(0)]
    public ChatContextMetadata Metadata { get; }

    [IgnoreMember]
    public int MessageCount => branchNodes.Count;

    /// <summary>
    /// For MessagePack (de)serialization, the message nodes are stored in a dictionary for efficient access.
    /// </summary>
    [Key(1)]
    private ICollection<ChatMessageNode> MessageNodes => messageNodeMap.Values;

    [Key(2)]
    private readonly ChatMessageNode rootNode;

    [IgnoreMember] private readonly Dictionary<Guid, ChatMessageNode> messageNodeMap = new();

    /// <summary>
    /// nodes on the current branch. [0] will always be the root node (System Prompt).
    /// </summary>
    [IgnoreMember] private readonly ObservableList<ChatMessageNode> branchNodes = [];

    private ChatContext(ChatContextMetadata metadata, ICollection<ChatMessageNode> messageNodes, ChatMessageNode rootNode)
    {
        Metadata = metadata;
        messageNodeMap.AddRange(messageNodes.Select(v => new KeyValuePair<Guid, ChatMessageNode>(v.Id, v)));
        this.rootNode = rootNode;
        rootNode.Context = this;
        rootNode.PropertyChanged += OnNodePropertyChanged;
        branchNodes.Add(rootNode);

        foreach (var node in messageNodes.Append(rootNode))
        {
            node.Context = this;
            node.PropertyChanged += OnNodePropertyChanged;
            foreach (var childId in node.Children) messageNodeMap[childId].Parent = node;
        }

        UpdateBranchAfter(0, rootNode);
    }

    public ChatContext(string systemPrompt)
    {
        Metadata = new ChatContextMetadata
        {
            DateCreated = DateTimeOffset.UtcNow,
            DateModified = DateTimeOffset.UtcNow,
        };
        rootNode = ChatMessageNode.CreateRootNode(systemPrompt);
        rootNode.PropertyChanged += OnNodePropertyChanged;
        branchNodes.Add(rootNode);
    }

    public NotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ToNotifyCollectionChanged(
        Action<ISynchronizedView<ChatMessageNode, ChatMessageNode>>? transform,
        ICollectionEventDispatcher? collectionEventDispatcher) =>
        transform is not null ?
            branchNodes.CreateView(x => x).With(transform).ToNotifyCollectionChanged(collectionEventDispatcher) :
            branchNodes.ToWritableNotifyCollectionChanged(collectionEventDispatcher);

    /// <summary>
    /// Create a new branch on the specified sibling node.
    /// </summary>
    /// <param name="siblingNode"></param>
    /// <param name="chatMessage"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void CreateBranchOn(ChatMessageNode siblingNode, ChatMessage chatMessage)
    {
        var index = branchNodes.IndexOf(siblingNode);
        if (index < 0)
        {
            throw new ArgumentException("The specified node is not in the current branch.", nameof(siblingNode));
        }

        Insert(index, chatMessage);
    }

    public void Insert(int index, ChatMessage chatMessage) => Insert(index, new ChatMessageNode(chatMessage)
    {
        Context = this
    });

    /// <summary>
    /// Add a message to the end of the current branch.
    /// </summary>
    /// <param name="message"></param>
    public void Add(ChatMessage message)
    {
        Insert(branchNodes.Count, new ChatMessageNode(message)
        {
            Context = this
        });
    }

    public IEnumerator<ChatMessageNode> GetEnumerator()
    {
        return branchNodes.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)branchNodes).GetEnumerator();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Metadata.DateModified = DateTimeOffset.UtcNow;

        if (e.PropertyName != nameof(ChatMessageNode.ChoiceIndex)) return;
        UpdateBranchAfterNode(sender.NotNull<ChatMessageNode>());
    }

    /// <summary>
    /// Update the branch after a specific node. This will be called when a node's choice index is changed.
    /// </summary>
    /// <param name="node"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void UpdateBranchAfterNode(ChatMessageNode node)
    {
        UpdateBranchAfter(branchNodes.IndexOf(node), node);
    }

    private void UpdateBranchAfter(int index, ChatMessageNode node)
    {
        Debug.Assert(index != -1, "Node is not in the branch nodes.");

        for (var i = branchNodes.Count - 1; i > index; i--)
        {
            branchNodes.RemoveAt(i);
        }

        // Add nodes after the specified node.
        while (true)
        {
            if (node.ChoiceIndex < 0 || node.ChoiceIndex >= node.Children.Count) break;
            branchNodes.Add(node = messageNodeMap[node.Children[node.ChoiceIndex]]);
        }
    }

    private void Insert(int index, ChatMessageNode newNode)
    {
        var afterNode = index switch
        {
            0 => rootNode,
            _ => branchNodes[index - 1]
        };
        afterNode.Children.Add(newNode.Id);
        newNode.Parent = afterNode;
        messageNodeMap[newNode.Id] = newNode;
        newNode.PropertyChanged += OnNodePropertyChanged;
        afterNode.ChoiceIndex = afterNode.Children.Count - 1;
        UpdateBranchAfter(index - 1, afterNode);
    }
}

[MessagePackObject(AllowPrivate = true)]
public partial class ChatContextMetadata : ObservableObject
{
    [Key(0)]
    public DateTimeOffset DateCreated { get; set; }

    [Key(1)]
    [ObservableProperty]
    public partial DateTimeOffset DateModified { get; set; }

    [Key(2)]
    [ObservableProperty]
    public partial string? Topic { get; set; }
}

/// <summary>
/// chat history is a tree structure
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public partial class ChatMessageNode : ObservableObject
{
    [Key(0)]
    public Guid Id { get; }

    [Key(1)]
    public ChatMessage Message { get; }

    [Key(2)]
    public ObservableList<Guid> Children { get; }

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
    /// Constructor for MessagePack deserialization.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="message"></param>
    /// <param name="children"></param>
    private ChatMessageNode(Guid id, ChatMessage? message, ObservableList<Guid>? children)
    {
        Id = id;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        message.PropertyChanged += OnMessagePropertyChanged;
        Children = children ?? throw new ArgumentNullException(nameof(children));
        children.CollectionChanged += OnChildrenChanged;
    }

    public ChatMessageNode(ChatMessage message) : this(Guid.CreateVersion7(), message, []) { }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Message));
    }

    private void OnChildrenChanged(in NotifyCollectionChangedEventArgs<Guid> e)
    {
        OnPropertyChanged(nameof(ChoiceCount));
    }

    internal static ChatMessageNode CreateRootNode(string systemPrompt)
    {
        return new ChatMessageNode(Guid.Empty, new SystemChatMessage(systemPrompt), []);
    }
}