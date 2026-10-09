namespace BmadPlatform.Application.Features.Assistant.Script;

/// <summary>What a script topic asks of the user and how it counts toward progress.</summary>
public enum TopicKind
{
    /// <summary>Open question. Free text or a quick reply covers it, including "No sé".</summary>
    Question,

    /// <summary>Closed question (sizing). Only a quick reply covers it; free text is re-asked.</summary>
    Choice,

    /// <summary>Proposal of a depth level. Never covered by history: it is asked while the level is empty.</summary>
    DepthProposal,

    /// <summary>The explicit step from Aclarar to Planificar. The initiative status closes it, not history.</summary>
    Confirmation,

    /// <summary>The last message once everything required is covered.</summary>
    Closing,
}
