using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

// What a chat is looking at. Multiple rows = multi-target chat (a note plus its
// linked scribbles plus a PDF). ChatSession.ScopeId/ScopeType stays as the
// "owning" location used for listing sessions.
public class ChatSessionScope
{
    public Guid ChatSessionId { get; set; }
    public ChatScopeType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public bool IncludeLinked { get; set; } = true;   // note <-> its scribbles
    public bool IncludeAncestors { get; set; }        // also pull the surrounding folder/workspace
}