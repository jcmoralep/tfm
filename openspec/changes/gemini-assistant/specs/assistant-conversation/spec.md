# Delta for Assistant Conversation (gemini-assistant)

**Baseline.** The capability `assistant-conversation` is NOT in `openspec/specs/` yet. The baseline of this delta is `openspec/changes/assistant-conversation/specs/assistant-conversation/spec.md` (status done). BMAD has no archive or global spec merge, so assistant-conversation is not archived: every MODIFIED requirement below is written in FULL (complete replacement text, same name as in the baseline) and replaces the baseline requirement of the same name; every requirement of the baseline that is not named here stays as it is. Source requirements: RF-10, RF-12, RF-13, RF-15, RF-35, RF-41, RF-42, RF-45 to RF-50, RF-65, RF-66, RF-69, RF-70 (REQUISITOS.md; RF-65 and RF-66 are the conduct and sufficiency authority). Companion specs: `gemini-client` (HTTP contract, reason codes) and `assistant-provider-configuration` (switch).

Scenario tags: **[UNIT]** xUnit at domain, validator, handler or pure-class level (hand-written doubles including a scripted assistant double and a stub model client; no bUnit, no network, no key). **[UI]** manual-only check in Chrome. **[DOC]** documentation deliverable checked by reading the file. **[MANUAL]** needs the live key; outside CI (persona evaluation, Chrome walkthrough). Terminology as in the baseline. Added terms: the **verdict** of a model reply is `Proceed` or `NeedsMore`. A **follow-up** is an assistant message that carries the topic key of a Question topic whose original question was already asked (the pushback). The **follow-up count** of a topic is the number of visible assistant messages with its key minus one. A **model turn** is a turn whose wording is requested from the model: only the asking of a Question topic and its follow-up. **Script wording** is the Fake's text for a turn. Message wording is a proposal unless marked verbatim.

## ADDED Requirements

### Requirement: Script owns the flow; the model only words Question turns (RF-15, RF-68)

The script MUST keep ownership of topics, order, quick replies, level, mode, depth, confirmation and closing. The model MUST be called only to word the asking of a Question topic (and its single follow-up, see "One pushback per topic"). The model MUST NOT be called on Choice topics (sizing), the depth proposal, the confirmation, the closing ("lista para redactar" and the Planificar-complete statement) or the re-ask after free text on a Choice topic; those turns MUST use the script wording and MUST cost no model call. At most ONE model call MUST be made per assistant turn. The model MUST NOT emit topic keys, quick replies or levels: the application stores every reply under the key it computed and composes the quick replies itself, with "No sé" on every Question topic. A depth suggestion MUST come from the script mapping of the sizing answer; a `SuggestedDepth` from the model MUST never be applied.

#### Scenario: Model is called for a Question topic [UNIT]
- GIVEN a stub model client and the next topic is the users question
- WHEN the assistant turn is built
- THEN exactly one model call is made

#### Scenario: No model call on non-Question turns [UNIT]
- GIVEN turns for the sizing choice, the depth proposal, the confirmation, the "lista para redactar" closing and the Planificar-complete statement
- WHEN each is built with a model client that fails the test if called
- THEN each uses script wording and the model client is never called

#### Scenario: Free text on a Choice topic is re-asked without the model [UNIT]
- GIVEN the current topic is the confirmation and the user sends free text
- WHEN the assistant re-asks
- THEN the re-ask is script wording and no model call is made

#### Scenario: Quick replies come from the script [UNIT]
- GIVEN a model reply for a Question topic
- WHEN the message is stored
- THEN its quick replies are the script's, include "No sé", and none comes from the model

#### Scenario: Depth is never taken from the model [UNIT]
- GIVEN a model reply on a Question turn that includes a `SuggestedDepth` of Large
- WHEN the turn is processed
- THEN mode and depth are unchanged

### Requirement: Model-worded questions in the Fake's layout (RF-41, RF-65 a-c, RF-35)

When the model words a Question turn, its JSON reply MUST be `{verdict, acknowledgement, question, example, why}` and code MUST assemble the stored text in the Fake's layout, in this order: the acknowledgement, the question, "Por ejemplo:" and the example, "Por qué lo pregunto:" and the reason. Exactly one question MUST be asked per turn, about the topic the application computed. The acknowledgement MUST be neutral (what was understood) and MUST NOT praise. The example MUST be tailored to the initiative, offered as a best answer or hypothesis the person can accept or correct. The register MUST be "usted", the language Spanish, the vocabulary plain business Spanish without jargon, and the text plain (no Markdown, no lists of questions). On the opening turn there is no previous answer, so there is no acknowledgement. The instructions to the model are written in English with the Spanish output rules; the Spanish text is the only user-facing text.

#### Scenario: Assembly follows the layout [UNIT]
- GIVEN a stub reply with acknowledgement "A", question "Q", example "E" and why "W"
- WHEN the text is assembled
- THEN the stored text contains A, then Q, then "Por ejemplo: E", then "Por qué lo pregunto: W", in that order

#### Scenario: Opening turn has no acknowledgement [UNIT]
- GIVEN the first turn of a conversation and a stub reply
- WHEN the text is assembled
- THEN it starts with the question of the idea topic and has an example and a reason

#### Scenario: One question per turn, about the computed topic [UNIT]
- GIVEN the computed next topic is "success"
- WHEN the reply is stored
- THEN it is stored under the key of "success" and the stored text has exactly one question part

#### Scenario: Tailored examples, "usted", no praise [MANUAL]
- GIVEN the persona evaluation script with a live key (a junior business user, a vague user, a contradicting user)
- WHEN every Question turn is read
- THEN examples mention the person's own initiative, the register is "usted", and no turn praises ("excelente", "muy buena respuesta")

### Requirement: One pushback per topic (RF-69, RF-65 d-e, RF-35)

After a free-text answer to a Question topic the model MUST return a verdict. `NeedsMore` MUST mean that the answer is thin, vague or contradicts an earlier answer (RF-65 d and e); then the reply text MUST be a follow-up for the SAME topic (acknowledgement of what was understood, the missing piece as one question, an example, a reason) and MUST be stored under the key of the answered topic, so the topic becomes uncovered and the next topic stays the same. `Proceed` MUST mean the answer is enough: the reply is the wording of the next topic and is stored under the next key. There MUST be at most ONE follow-up per topic, contradictions included; code enforces it by counting visible assistant messages with the topic key, never the model. A free-text answer is eligible only when the follow-up count is zero. A "No sé" answer (quick reply or typed phrase) and any quick-reply answer MUST never be eligible; their verdict MUST be treated as `Proceed` whatever the model returns, because "No sé" is not a weak answer (RF-65 g). An ineligible `NeedsMore` MUST be treated as `Proceed`. The answer to a follow-up covers the topic whatever it says (a second thin answer is accepted). The Fake MUST always return `Proceed`; the reply type MUST default the verdict to `Proceed`.

#### Scenario: Thin answer gets one follow-up [UNIT]
- GIVEN a scripted double that returns `NeedsMore` and a free-text answer on the idea topic with no earlier follow-up
- WHEN the reply is stored
- THEN it is an assistant message with the idea key, the idea topic is uncovered and the next topic is still the idea

#### Scenario: Sufficient answer proceeds [UNIT]
- GIVEN a scripted double that returns `Proceed`
- WHEN the free-text answer to the users topic is stored
- THEN the reply is stored under the next topic's key and users is covered

#### Scenario: Second answer is accepted whatever it says [UNIT]
- GIVEN an idea answer, its follow-up, and a second free-text answer for which the double returns `NeedsMore`
- WHEN the reply is stored
- THEN the idea topic is covered, the verdict is treated as `Proceed`, and the reply is for the next topic

#### Scenario: Cap counts visible assistant messages with the key [UNIT]
- GIVEN visible messages: question (idea), answer, follow-up (idea)
- WHEN eligibility is evaluated for the next idea answer
- THEN the follow-up count is 1 and the answer is not eligible

#### Scenario: "No sé" is never pushed [UNIT]
- GIVEN a "No sé" quick reply, and a typed "ni idea", each with a double that returns `NeedsMore`
- WHEN each reply is stored
- THEN the topic is covered with the don't-know marker, no follow-up is stored and the reply is for the next topic

#### Scenario: Quick-reply answers are never pushed [UNIT]
- GIVEN a quick reply such as "Ninguno" on a Question topic and a double that returns `NeedsMore`
- WHEN the reply is stored
- THEN the topic is covered and no follow-up is stored

#### Scenario: "No sé" after a follow-up closes the topic [UNIT]
- GIVEN an idea answer and its follow-up
- WHEN the user answers the follow-up with "No sé"
- THEN the topic is covered with the don't-know marker and the next topic follows

#### Scenario: Contradiction shares the cap [UNIT]
- GIVEN a problem topic that already had a thin-answer follow-up, and a second answer that contradicts the idea, with a double returning `NeedsMore` with a contradiction reason
- WHEN the reply is stored
- THEN no second follow-up is stored for the problem topic and the reply is for the next topic

#### Scenario: Contradiction gets the one follow-up [UNIT]
- GIVEN a success answer that contradicts the problem answer and a double returning `NeedsMore`
- WHEN the reply is stored
- THEN one follow-up for the success topic is stored and the topic is uncovered

#### Scenario: Reload does not call the model again [UNIT]
- GIVEN the last visible message is a follow-up
- WHEN the conversation is read or started again, from this or another tab
- THEN no model call is made and no message is added

#### Scenario: Progress dips while a follow-up is pending [UNIT]
- GIVEN three covered topics and a follow-up on a fourth that was answered thinly
- WHEN progress is derived
- THEN the text reads "3 de N preguntas respondidas" (the thin topic is not counted) and after the user answers the follow-up it reads "4 de N preguntas respondidas"

#### Scenario: Pushback on a thin idea in the real model [MANUAL]
- GIVEN a live key and the idea answer "mejorar el reporte de ventas, está feo"
- WHEN the reply arrives
- THEN it is one follow-up on the idea asking for a missing piece (what part changes, who uses it or why now), without praise and without suggesting another level

### Requirement: Reply guard (RF-41, RF-65, RF-69)

Every model reply MUST pass a deterministic `ReplyGuard` before it is used. The guard MUST reject a reply when: the verdict is not `Proceed` or `NeedsMore`; `question`, `example` or `why` is empty or whitespace (and `acknowledgement` is empty when an answer precedes the turn); the assembled text exceeds 4,000 characters; the text contains forbidden jargon, at least "spine", "épica", "invariante" and "slug" (case and accents ignored, whole words or their plurals); a `SuggestedDepth` is present and is not a defined level. Before the length check, the guard MUST strip Markdown and HTML markup (emphasis, headings, list markers, code, links, tags) from every field so the stored message is plain text. A rejected reply MUST be handled exactly like malformed output (see "Fallback to script wording"). The guard cannot detect praise or a wrong question; those are covered by the prompt and the manual evaluation.

#### Scenario: Empty field is rejected [UNIT]
- GIVEN replies with an empty question, an empty example and an empty why
- WHEN each passes the guard
- THEN each is rejected

#### Scenario: Over 4,000 characters is rejected, exactly 4,000 passes [UNIT]
- GIVEN assembled texts of 4,000 and 4,001 characters
- WHEN each passes the guard
- THEN the first passes and the second is rejected

#### Scenario: Jargon is rejected [UNIT]
- GIVEN replies containing "Spine", "las épicas", "un slug" and "invariantes"
- WHEN each passes the guard
- THEN each is rejected, while "espina dorsal" is accepted

#### Scenario: Unknown verdict or depth is rejected [UNIT]
- GIVEN a verdict "Maybe" and a `SuggestedDepth` of 99
- WHEN each passes the guard
- THEN each is rejected

#### Scenario: Markdown is stripped [UNIT]
- GIVEN a reply with `**negrita**`, a `# título`, a list marker and a link
- WHEN it passes the guard
- THEN the stored text has the words without the markup

### Requirement: Fallback to script wording on blocked or malformed output (RF-70, RF-15)

When the model call ends with reason Blocked or Malformed, or the reply fails the guard, the turn MUST fall back to the script wording for that turn: the same text the Fake would produce, stored under the key the application computed, with the verdict treated as `Proceed` (no follow-up). The conversation MUST NOT stop and no error MUST be shown for Malformed or a guard rejection. For Blocked, the script text MUST be followed by a short sentence saying that the person can undo the last answer and rephrase it ("Si lo prefiere, puede deshacer su última respuesta y escribirla de otra forma."). A fallback MUST NOT count as a follow-up and MUST NOT be retried automatically. The fallback MUST be logged with the topic key and the reason only.

#### Scenario: Malformed output falls back silently [UNIT]
- GIVEN a model client that fails with reason Malformed
- WHEN an answer is sent
- THEN the stored reply equals the Fake's reply for the next topic and no error is shown

#### Scenario: Guard rejection falls back [UNIT]
- GIVEN a model reply containing "épica"
- WHEN the turn is processed
- THEN the stored reply equals the script wording

#### Scenario: Blocked output falls back with guidance [UNIT]
- GIVEN a model client that fails with reason Blocked
- WHEN an answer is sent
- THEN the stored reply is the script wording followed by the undo-and-rephrase sentence

#### Scenario: Fallback is not a follow-up [UNIT]
- GIVEN a fallback reply for the next topic
- WHEN the follow-up count of the answered topic is derived
- THEN it is zero

#### Scenario: Fallback log has no content [UNIT]
- GIVEN a Blocked fallback with a capturing logger and a distinctive answer text
- WHEN the entries are inspected
- THEN they hold the topic key and the reason, and not the text

### Requirement: Per-topic sufficiency hints (RF-66, RF-65 d)

Each Question topic MUST carry a `SufficiencyHint` as script data, sent to the model with the topic and used to judge the verdict. The hints MUST state the minimum that BMAD asks (REQUISITOS RF-66), in plain terms:
- idea: what it is, what it does for the user, why it matters (and, in Small, which part changes, who uses it and why now);
- users: at least one job to do, in the person's own words;
- problem: what goes wrong today and for whom;
- capabilities: each one with at least one checkable consequence;
- constraints and non-functional qualities: bounds, not adjectives;
- success: a measure with a target and, when there are metrics, one counter-metric;
- out of scope: at least one item;
- integrations and teams: the system or team and what is exchanged.
Minima for sections the script never asks (a named-person journey, non-users, glossary) MUST NOT be asked in this change; they remain an assumption or an open question in the documents.

#### Scenario: Every Question topic has a hint [UNIT]
- GIVEN the full script
- WHEN every Question topic is inspected
- THEN each has a non-empty `SufficiencyHint`, and Choice topics have none

#### Scenario: Hint travels with the request [UNIT]
- GIVEN a model turn for the success topic
- WHEN the request to the client is captured
- THEN the transcript or instruction carries the success hint

#### Scenario: Unasked minima are not asked [UNIT]
- GIVEN the full script
- WHEN its topics are listed
- THEN there is no topic for a named journey, non-users or glossary

#### Scenario: Metric without target gets a follow-up [MANUAL]
- GIVEN a live key and the success answer "que los usuarios estén contentos"
- WHEN the reply arrives
- THEN the follow-up asks how they would measure it and mentions a target

### Requirement: Conduct rules mapped to checks (RF-65)

The assistant's conduct MUST follow RF-65. Enforcement MUST be: (a) one question per turn and in dependency order: the script owns the order and code assembles exactly one question; (b) a best answer or hypothesis: the example is required by the guard and tailored by the prompt; (c) no praise: prompt only, checked in the manual evaluation; (d) pressing on weak answers: the verdict and the one-follow-up cap; (e) resolving contradictions: a `NeedsMore` verdict with a contradiction reason, sharing the cap; (f) never inventing: assistant text only asks, and the drafter tags assumptions (see `artifacts`); (g) "No sé" becomes an open question without pressure: never eligible for a follow-up, and the drafter creates the Q-n.

#### Scenario: (a) one question in dependency order [UNIT]
- GIVEN a Standard conversation answered in order with `Proceed` verdicts
- WHEN the stored assistant messages are listed
- THEN each asks one topic and the order equals the script order

#### Scenario: (b) example is required [UNIT]
- GIVEN a reply with an empty example
- WHEN it passes the guard
- THEN it is rejected and the script wording (with its example) is used

#### Scenario: (c) no praise [MANUAL]
- GIVEN the persona evaluation with a live key
- WHEN all replies are read
- THEN none praises the person

#### Scenario: (d)(e) weak and contradicting answers [UNIT]
- GIVEN the scenarios of "One pushback per topic"
- WHEN they run
- THEN weak and contradicting answers each get at most one follow-up

#### Scenario: (g) "No sé" is recorded, not pressed [UNIT]
- GIVEN a topic answered "No sé"
- WHEN the reply is stored and later the PRD is drafted
- THEN there is no follow-up and the document has a Q-n for that topic

## MODIFIED Requirements

### Requirement: Script per level (RF-35, RF-46, RF-42)

The script MUST be data of the platform (BMAD-inspired, not BMAD-verbatim). The required set MUST be computed from the initiative's current mode and depth. The next topic MUST be the first topic, in the order below, that is in the required set and not covered. Order:

| Level | Aclarar | Planificar |
|---|---|---|
| Small | idea, users, problem, capabilities, out of scope, success, confirmation | none |
| Standard | idea, users, problem, success, out of scope, [sizing], confirmation | capabilities, constraints, priorities |
| Large | same Aclarar as Standard | capabilities, constraints, priorities, integrations and teams, non-functional qualities |

The sizing step is in the required set only while mode is Automatic and depth is empty; until then the Aclarar part of the Standard list is used. Counts exclude the confirmation, which is a separate step shown when the total is reached: Small 6, Standard 8, Large 10. A follow-up never changes the counts. Each topic MUST be asked on its own turn, with a short plain explanation of why it is asked, an example, and quick replies. Question topics MUST offer "No sé" as a quick reply. For Small, RF-42's three basic questions (what part changes, who uses it, why now) are covered by AT LEAST the idea, users and problem topics within its 6 questions; no extra topic is added. When the idea answer is thin, the assistant MAY use the single follow-up of "One pushback per topic" on the idea to ask for the missing piece, and MUST then continue with the next Small topic. No question MUST redirect the user to another level (RF-42).
(Previously: RF-42 was met by continuing with the next Small topic, with no pushback on a thin idea.)

#### Scenario: Small asks six topics, no Planificar [UNIT]
- GIVEN a Small initiative
- WHEN every topic is answered in turn with `Proceed` verdicts
- THEN the topics asked are idea, users, problem, capabilities, out of scope, success, then the confirmation, and no Planificar topic is ever asked

#### Scenario: Standard order [UNIT]
- GIVEN a Standard (Manual) initiative
- WHEN every topic is answered in turn
- THEN the order is idea, users, problem, success, out of scope, confirmation, then capabilities, constraints, priorities, with no sizing step

#### Scenario: Large order [UNIT]
- GIVEN a Large initiative
- WHEN every topic is answered in turn
- THEN the Planificar part adds integrations and teams, then non-functional qualities after priorities, for 10 questions in total, not counting the confirmation

#### Scenario: One question per turn with example [UNIT]
- GIVEN any topic of any level
- WHEN the assistant turn for it is built (Fake or model-worded)
- THEN it asks exactly that topic, contains an example and a short reason, and offers at least one quick reply

#### Scenario: Thin Small idea gets one pushback, not a redirect [UNIT]
- GIVEN a Small initiative whose idea answer is "mejorar el reporte de ventas, está feo" and a double returning `NeedsMore`
- WHEN the assistant replies
- THEN the reply is the single follow-up on the idea, no text suggests another level, and after the next answer the next topic is users

#### Scenario: Small count stays six with a follow-up [UNIT]
- GIVEN a Small conversation where the idea received a follow-up
- WHEN progress is derived after all six topics are answered
- THEN it reads "6 de 6 preguntas respondidas" and the confirmation is next

#### Scenario: RF-42's three basics are inside the six [UNIT]
- GIVEN the Small script
- WHEN its topics are listed
- THEN idea, users and problem are among the six

#### Scenario: Sizing only in Automatic without level [UNIT]
- GIVEN initiatives (a) Automatic with empty depth, (b) Manual Standard, (c) Manual Small
- WHEN the required sets are computed
- THEN only (a) contains the sizing step

### Requirement: "No sé" answers (RF-35, RF-65 g)

Choosing the "No sé" quick reply MUST be accepted as a valid answer, MUST cover its topic, and MUST be recorded on the stored user message so it can be told apart from other answers. The assistant MUST NOT re-ask a topic answered with "No sé" and MUST NOT push back on it: a "No sé" answer is never eligible for a follow-up, including the answer to a follow-up. The document later records the topic as an open question.
(Previously: nothing was said about follow-ups, because no pushback existed.)

#### Scenario: No sé covers the topic and is marked [UNIT]
- GIVEN a question topic
- WHEN the user chooses "No sé"
- THEN the user message is stored with a don't-know marker, the topic counts as covered and the next topic is asked

#### Scenario: Typed don't-know phrase counts as the button [UNIT]
- GIVEN a question topic
- WHEN the whole trimmed message is "no sé", "no se", "ni idea" or "no lo sé" (case and accents ignored)
- THEN it is stored with the don't-know marker like the "No sé" quick reply and covers the topic

#### Scenario: Longer text containing the phrase is a normal answer [UNIT]
- GIVEN a question topic
- WHEN the user types "no sé, creo que es SAP"
- THEN it is stored as an ordinary answer without the don't-know marker, and it is eligible for the one follow-up

#### Scenario: No sé is never followed up [UNIT]
- GIVEN a "No sé" answer and a double that returns `NeedsMore`
- WHEN the reply is stored
- THEN no follow-up exists for that topic

### Requirement: Derived progress and visible coverage (RF-10, RF-50, RF-69)

Progress MUST be derived on read from visible messages and MUST NOT depend on stored counters. A Question topic is covered if and only if the LAST visible message with its topic key is a user answer; therefore a pending follow-up (a later assistant message with that key) makes the topic uncovered. Any valid answer covers, including "No sé" and quick replies. For the confirmation only "Sí, pasar a Planificar" covers; for the sizing step only once the depth is set; the depth proposal is never covered by history. "Quiero añadir algo" does not cover the confirmation. Answers to topics outside the current required set MUST be kept but MUST NOT count. The progress value is covered required topics out of required topics, and the visible text MUST be "{covered} de {total} preguntas respondidas" (assumption on wording). Total excludes the confirmation and, in Planning, includes the Planificar topics. While a follow-up is pending, covered MUST be one lower than before the thin answer (the dip is expected and is not a regression).
(Previously: a topic was covered by any visible answer with its key.)

#### Scenario: Progress after answers [UNIT]
- GIVEN a Standard conversation with the idea and users answered
- WHEN progress is derived
- THEN it is 2 of 8 and the text is "2 de 8 preguntas respondidas"

#### Scenario: No sé counts [UNIT]
- GIVEN a "No sé" answer to the problem topic
- WHEN progress is derived
- THEN the problem topic counts as covered

#### Scenario: Extra answers do not count [UNIT]
- GIVEN a Large conversation with integrations answered, then the level changed to Standard
- WHEN progress is derived
- THEN the integrations answer is retained but excluded from covered and total

#### Scenario: Pending follow-up uncovers the topic [UNIT]
- GIVEN visible messages: question (idea), answer, follow-up (idea)
- WHEN coverage is derived
- THEN the idea topic is not covered and is the next topic

#### Scenario: Answering the follow-up covers again [UNIT]
- GIVEN the messages above plus a user answer with the idea key
- WHEN coverage is derived
- THEN the idea topic is covered

#### Scenario: Last message decides, not any message [UNIT]
- GIVEN visible messages for a key in the order answer, assistant message
- WHEN coverage is derived
- THEN the topic is not covered, whatever earlier answers exist

#### Scenario: Choice topics keep their rules [UNIT]
- GIVEN the sizing question answered by free text and then by a quick reply
- WHEN coverage is derived after each
- THEN the sizing step is covered only after the quick reply sets the depth

#### Scenario: Progress panel [UI]
- GIVEN the chat page
- WHEN it renders
- THEN the journey panel lists Aclarar, Planificar and Lista para construir (Small omits Planificar), marks the current phase and shows the progress text

### Requirement: Undo last answer (RF-49, RF-69)

The system MUST offer "Deshacer mi última respuesta". Invoking it MUST make the last visible user answer and every assistant message that followed it (the reaction, whether the next question or a follow-up) stop being visible, make the question that preceded the answer reappear as the current question, and recompute progress and the follow-up count. An answer and its follow-up therefore disappear together, and a topic whose follow-up was hidden may receive a new follow-up when answered again. Only the last visible answer MAY be undone. Opening and other assistant messages MUST never be undone alone. An answer that changed the initiative MUST NOT be undone: the confirmation "Sí, pasar a Planificar" and, by assumption, an accepted depth change. Repeated invocation undoes the then-last visible answer, one per call, until none is left or a non-undoable answer is last. Refusals (verbatim): "No se puede deshacer una respuesta que ya cambió el estado de la iniciativa." and, with nothing to undo, "No hay ninguna respuesta que deshacer." Whether undone messages are hidden or deleted is a design decision.
(Previously: the reaction was always the next question; follow-ups did not exist.)

#### Scenario: Undo last answer [UNIT]
- GIVEN answers A1, A2 and A3 with their reactions
- WHEN the user undoes
- THEN A3 and its reaction are not visible, the A3 question is the current one and progress counts A1 and A2 only

#### Scenario: Undo right after the first answer [UNIT]
- GIVEN only the opening question and one answer
- WHEN the user undoes
- THEN the opening question remains as the current question and progress is 0

#### Scenario: Repeated undo walks back one answer each [UNIT]
- GIVEN answers A1 and A2
- WHEN the user undoes twice
- THEN both are hidden in order, and a third call is rejected with "No hay ninguna respuesta que deshacer."

#### Scenario: Undo with nothing to undo [UNIT]
- GIVEN a conversation with only the opening question
- WHEN the user undoes
- THEN it is rejected with "No hay ninguna respuesta que deshacer." and nothing changes

#### Scenario: Confirmation answer cannot be undone [UNIT]
- GIVEN the last answer is "Sí, pasar a Planificar" and the initiative is Planning
- WHEN the user undoes
- THEN it is rejected with "No se puede deshacer una respuesta que ya cambió el estado de la iniciativa." and the status stays Planning

#### Scenario: Answer after the confirmation can be undone, the confirmation cannot [UNIT]
- GIVEN the confirmation then one Planificar answer
- WHEN the user undoes twice
- THEN the first call succeeds and the second is rejected as above

#### Scenario: Accepted depth cannot be undone [UNIT]
- GIVEN the last answer accepted a suggested depth
- WHEN the user undoes
- THEN it is rejected with the same refusal and mode and depth are unchanged

#### Scenario: Undo with a pending reply [UNIT]
- GIVEN the last message is a user answer whose reply failed
- WHEN the user undoes
- THEN that answer is hidden and no reply is requested

#### Scenario: Undo by another user or on a locked state [UNIT]
- GIVEN a conversation of A
- WHEN B undoes, or A undoes on a Draft or ReadyToBuild initiative
- THEN B gets not found and A gets the eligibility rejection

#### Scenario: Undo the original answer hides its follow-up with it [UNIT]
- GIVEN visible messages: question (idea), answer, follow-up (idea)
- WHEN the user undoes
- THEN the answer and the follow-up are hidden, the idea question is the current one, the follow-up count is zero and progress is recomputed

#### Scenario: Undo the answer to a follow-up [UNIT]
- GIVEN question, answer, follow-up, second answer and the next topic's question
- WHEN the user undoes
- THEN the second answer and the next question are hidden and the follow-up is the current message, so the topic is uncovered with its follow-up count still 1

#### Scenario: A hidden follow-up may be asked again [UNIT]
- GIVEN an undo of an answer that had a follow-up, and a new thin answer with a double returning `NeedsMore`
- WHEN the reply is stored
- THEN a new follow-up is stored for the topic (the count was reset by the undo)

### Requirement: Guided mode only (RF-48)

The assistant MUST offer only the guided, step-by-step mode, whichever provider is configured. The UI MUST NOT show a fast-mode option. Fast mode (RF-12) is not part of the Gemini integration: it is enabled together with stakes calibration in a later change recorded in `docs/mejoras-posteriores.md`; until then RF-48 MUST say that only the guided mode exists and MUST NOT tie the fast mode to the Gemini integration.
(Previously: RF-48 enabled the fast mode "with the real AI integration".)

#### Scenario: No fast mode with the fake [UI]
- GIVEN the chat page with `Assistant:Provider` = Fake
- WHEN it renders
- THEN no fast-mode control is present

#### Scenario: No fast mode with Gemini [UI]
- GIVEN the chat page with `Assistant:Provider` = Gemini
- WHEN it renders
- THEN no fast-mode control is present

#### Scenario: RF-48 is decoupled from Gemini [DOC]
- GIVEN REQUISITOS.md RF-48 and `docs/mejoras-posteriores.md`
- WHEN read
- THEN RF-48 says only guided mode exists until the fast-mode change, and the improvements file records fast mode with stakes calibration as that later change

### Requirement: Deterministic fake assistant (RF-15)

`IAssistantService` MUST keep a fake implementation that is a pure function of its request: the same request MUST yield an equal reply (text, quick replies, suggested depth, verdict). It MUST NOT use the clock, randomness or I/O, MUST NOT send data outside the process, and MUST always return the verdict `Proceed`. It is the default implementation (`Assistant:Provider` = Fake) and the source of the script wording used as fallback. The request MUST carry only text and level data (topic, level, mode, history texts, initiative name and description, the topic's `SufficiencyHint`) and MUST NOT carry ids, owner, email or timestamps; this holds for every implementation, including the Gemini one. The application, not the service, MUST own progress and the next topic; the service only phrases the turn and judges the last answer.
(Previously: the fake was the only implementation and the reply had no verdict.)

#### Scenario: Same input same output [UNIT]
- GIVEN two equal requests
- WHEN the fake replies to both
- THEN the replies are equal

#### Scenario: Request has no identifiers [UNIT]
- GIVEN the request type
- WHEN its members are inspected
- THEN none is a user id, owner id, email, initiative id or timestamp

#### Scenario: Only the next topic is asked [UNIT]
- GIVEN a request for the users topic
- WHEN the fake replies
- THEN the text asks about users, not another topic

#### Scenario: Fake never pushes back [UNIT]
- GIVEN any request, including a one-word answer
- WHEN the fake replies
- THEN the verdict is `Proceed`

#### Scenario: Reply verdict defaults to Proceed [UNIT]
- GIVEN a reply created without a verdict
- WHEN its verdict is read
- THEN it is `Proceed`

### Requirement: Assistant failure handling (RF-15, RF-70)

The user message MUST be persisted before the service is called. If the service fails with reason Quota, Timeout, Auth or Model, the user message MUST remain, no assistant message MUST be stored, and the user MUST see a reason-specific Spanish message; the chat MUST show "Reintentar" and what the person typed MUST stay saved (and visible in the history). A conversation whose last visible message is from the user has a pending reply; "Reintentar", the next start or a resume MUST produce exactly one reply for it (resuming does not duplicate the answer). Blocked and Malformed are not failures here: they fall back to the script wording (see "Fallback to script wording"). While a reply is pending, sending another answer MUST be rejected with "El asistente aún debe responder a su mensaje anterior. Vuelva a abrir la conversación para reintentar." (assumption). Messages: Timeout and the generic case (verbatim, unchanged): "El asistente no está disponible en este momento. Su mensaje quedó guardado; inténtelo de nuevo en unos minutos."; Quota (proposal): "El asistente alcanzó su límite de uso por ahora. Su mensaje quedó guardado; pulse Reintentar más tarde."; Auth and Model (proposal): "El asistente no está configurado correctamente. Su mensaje quedó guardado; avise a quien mantiene la plataforma." A 429 MUST NOT be retried automatically.
(Previously: a single message for any failure and no reason codes.)

#### Scenario: Service fails [UNIT]
- GIVEN a service double that throws the unavailable error with reason Timeout
- WHEN the user sends an answer
- THEN the answer is persisted, no reply exists, and the generic Spanish unavailable message is returned

#### Scenario: Quota shows its own message [UNIT]
- GIVEN a service double that throws the unavailable error with reason Quota
- WHEN the user sends an answer
- THEN the answer is persisted, no reply exists, and the quota message is returned

#### Scenario: Auth and Model share the configuration message [UNIT]
- GIVEN service doubles that throw reasons Auth and Model
- WHEN the user sends an answer in each case
- THEN the configuration message is returned and the answer is persisted

#### Scenario: Pending reply retried on resume [UNIT]
- GIVEN the last message is a user answer
- WHEN the conversation is started again with a working service
- THEN one assistant reply is added for the next topic

#### Scenario: Concurrent resume produces one reply [UNIT]
- GIVEN a pending reply and two concurrent starts
- WHEN both complete
- THEN exactly one reply is stored

#### Scenario: Quota failure is not retried automatically [UNIT]
- GIVEN a model client that fails with reason Quota
- WHEN the turn runs
- THEN the service is called once and no second call is made until the user retries

#### Scenario: Retry produces exactly one model call [UNIT]
- GIVEN a pending reply after a Timeout and a working model client
- WHEN the user retries
- THEN one model call is made and one reply is stored

#### Scenario: Send while a reply is in flight is queued [UI]
- GIVEN the assistant is still producing a reply
- WHEN the user sends another answer
- THEN the answer is queued in the chat and sent only after the previous reply is stored, in the order typed, and an accidental double submit stores it once

#### Scenario: Send with a stored pending reply [UNIT]
- GIVEN the last visible message is a user answer with no reply stored (service failed)
- WHEN a new answer arrives at the domain
- THEN the domain rejects it and nothing is stored; the UI first retries the pending reply

#### Scenario: Confirmation with failing service [UNIT]
- GIVEN the service fails on the confirmation reply
- WHEN the conversation is later resumed with a working service
- THEN the reply is produced and the initiative becomes Planning

#### Scenario: Reintentar after a forced failure [UI]
- GIVEN a quota or timeout failure forced with the real key (for example a wrong model name or an interrupted network)
- WHEN the chat shows the message and the user presses Reintentar
- THEN the typed answer is still in the history, and one reply appears once the service works

### Requirement: Privacy note (RN-05, RNF-04)

The chat MUST NOT show a demo-mode notice: on 2026-10-12 the product owner accepted the free-tier risk (RN-05) and the notice was removed from the interface. The risk stays documented in the README and in `AGENTS.md` §4.8, which MUST say that with `Assistant:Provider=Gemini` the conversation content is sent to Google under the free-tier terms (it may be used to improve its products and reviewed by people) and that only very basic information is used until a company model is connected. The deterministic fake MUST NOT send data outside the process. This requirement replaces the baseline requirement of the same name from `assistant-conversation`, which required the note to be always visible.

#### Scenario: No demo notice in the chat [UI]
- GIVEN any state of the chat page, including an empty conversation
- WHEN it renders
- THEN no "Modo de demostración" note is present

#### Scenario: Fake sends nothing outside the process [UNIT]
- GIVEN `Assistant:Provider=Fake`
- WHEN a conversation advances and a document is drafted
- THEN no HTTP request is made

#### Scenario: The documentation states the Gemini data use [DOC]
- GIVEN the README and `AGENTS.md` §4.8
- WHEN they are read
- THEN they state that Gemini free-tier content may be used by Google and that only basic information is used for now

## Assumptions

- A follow-up needs no new quick replies: it offers the Question topic's usual quick replies, including "No sé".
- A fallback reply (Blocked, Malformed or guard rejection) is not a follow-up; the next free-text answer to the same topic is not made eligible by it.
- A non-eligible `NeedsMore` is treated as `Proceed` without a second model call.
- The Blocked guidance sentence and the Quota and Auth/Model messages are proposals; their meaning, not their wording, is the requirement.
- Auth and Model failures show a configuration message because the person cannot fix them; the reason code stays in the log.

## Out of Scope

Fast mode, stakes calibration and the mixed-stakes rule (a later change); new script topics (named-person journey, non-users, glossary, differentiator, RF-64); attachments sent to the model; streaming; a privacy notice in the UI (RN-05).
