namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// What other modules may know about the assistant's answers: whether the Aclarar and Planificar topics of the
/// level are covered, and the latest visible answer per content topic in script order.
/// </summary>
public sealed record ConversationAnswers(bool ClarifyComplete, bool PlanComplete, IReadOnlyList<TopicAnswer> Answers);

/// <param name="TopicKey">The script key of the topic, one of the public topic keys of the script.</param>
/// <param name="Question">The question as asked, in plain Spanish.</param>
/// <param name="Text">The answer as typed, or the quick reply label.</param>
/// <param name="IsUnknown">True for the "No sé" quick reply and for typed phrases that mean the same.</param>
public sealed record TopicAnswer(string TopicKey, string Question, string Text, bool IsUnknown);
