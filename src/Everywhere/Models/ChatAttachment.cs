using Avalonia.Media.Imaging;
using Everywhere.Serialization;
using Lucide.Avalonia;
using MessagePack;

namespace Everywhere.Models;

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
[Union(0, typeof(ChatVisualElementAttachment))]
[Union(1, typeof(ChatTextAttachment))]
[Union(2, typeof(ChatImageAttachment))]
[Union(3, typeof(ChatFileAttachment))]
public abstract partial class ChatAttachment(DynamicResourceKey headerKey)
{
    public abstract LucideIconKind Icon { get; }

    [Key(0)]
    public DynamicResourceKey HeaderKey => headerKey;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatVisualElementAttachment(DynamicResourceKey headerKey, LucideIconKind icon, IVisualElement element)
    : ChatAttachment(headerKey)
{
    [Key(1)]
    public override LucideIconKind Icon => icon;

    [Key(2)]
    [MessagePackFormatter(typeof(VisualElementMessagePackFormatter))]
    public IVisualElement Element => element;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatTextAttachment(DynamicResourceKey headerKey, string text) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.Text;

    [Key(1)]
    public string Text => text;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatImageAttachment(DynamicResourceKey headerKey, Bitmap image) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.Image;

    [Key(1)]
    public Bitmap Image => image;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatFileAttachment(DynamicResourceKey headerKey, string filePath) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.File;

    [Key(1)]
    public string FilePath => filePath;
}