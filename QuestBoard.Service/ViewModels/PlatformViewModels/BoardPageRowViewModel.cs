using Microsoft.AspNetCore.Mvc.ModelBinding;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

/// <summary>
/// One page switch on the Platform board edit form. Only <see cref="Page"/> and
/// <see cref="Enabled"/> come from the posted form; everything else is worked out again on every
/// render, so a tampered post cannot change what the form claims.
/// </summary>
public class BoardPageRowViewModel
{
    public BoardPage Page { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public string Label { get; set; } = string.Empty;

    [BindNever]
    public bool Locked { get; set; }

    [BindNever]
    public string? LockedReason { get; set; }
}
