using System.Diagnostics.CodeAnalysis;
using Avalonia.Media.Imaging;
using Lucide.Avalonia;
using MessagePack;

namespace Everywhere.Models;

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
[Union(0, typeof(ChatVisualElementAttachment))]
[Union(1, typeof(ChatTextAttachment))]
[Union(2, typeof(ChatImageAttachment))]
[Union(3, typeof(ChatFileAttachment))]
public abstract partial class ChatAttachment(DynamicResourceKeyBase headerKey)
{
    public abstract LucideIconKind Icon { get; }

    [Key(0)]
    public DynamicResourceKeyBase HeaderKey => headerKey;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatVisualElementAttachment : ChatAttachment
{
    [Key(1)]
    public override LucideIconKind Icon { get; }

    /// <summary>
    /// Ignore this property during serialization because it should already be converted into prompts and shouldn't appear in history.
    /// </summary>
    [IgnoreMember]
    [field: AllowNull, MaybeNull]
    public IVisualElement Element =>
        field ?? throw new InvalidOperationException("Element is not set. This attachment should not be serialized without an element.");

    [SerializationConstructor]
    private ChatVisualElementAttachment(DynamicResourceKeyBase headerKey, LucideIconKind icon) : base(headerKey)
    {
        Icon = icon;
    }

    public ChatVisualElementAttachment(DynamicResourceKeyBase headerKey, LucideIconKind icon, IVisualElement element) : base(headerKey)
    {
        Icon = icon;
        Element = element;
    }
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatTextAttachment(DynamicResourceKeyBase headerKey, string text) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.Text;

    [Key(1)]
    public string Text => text;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatImageAttachment : ChatAttachment
{
    public override LucideIconKind Icon => LucideIconKind.Image;

    public Bitmap Image { get; }

    /// <summary>
    /// Encoded image data for serialization.
    /// </summary>
    [Key(1)]
    private byte[] ImageData
    {
        get
        {
            var stream = new MemoryStream();
            Image.Save(stream, 100);
            return stream.ToArray();
        }
    }

    [SerializationConstructor]
    private ChatImageAttachment(DynamicResourceKeyBase headerKey, byte[] imageData) : base(headerKey)
    {
        using var stream = new MemoryStream(imageData);
        Image = WriteableBitmap.Decode(stream);
    }

    public ChatImageAttachment(DynamicResourceKeyBase headerKey, Bitmap image) : base(headerKey)
    {
        Image = image;
    }
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatFileAttachment(DynamicResourceKeyBase headerKey, string filePath) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.File;

    [Key(1)]
    public string FilePath => filePath;
}