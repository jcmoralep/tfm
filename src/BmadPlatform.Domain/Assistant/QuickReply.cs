namespace BmadPlatform.Domain.Assistant;

/// <summary>A button offered with a question. The client sends only the key; the label is what the user saw.</summary>
public sealed record QuickReply(string Key, string Label);
