using AutoMapper;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Service.ViewModels.AgendaViewModels;
using QuestBoard.Service.ViewModels.QuestViewModels;
using QuestBoard.Service.ViewModels.ShopViewModels;
using QuestBoard.Service.ViewModels.CharacterViewModels;
using QuestBoard.Service.ViewModels.ContactViewModels;
using QuestBoard.Service.ViewModels.DungeonMasterViewModels;
using QuestBoard.Service.ViewModels.EventViewModels;
using QuestBoard.Service.ViewModels.SeriesViewModels;

namespace QuestBoard.Service.Automapper;

public class ViewModelProfile : Profile
{
    public ViewModelProfile()
    {
        CreateMap<QuestViewModel, Quest>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.ProposedDates, opt => opt.MapFrom(src => src.ProposedDates))
            .ForMember(dest => dest.DungeonMaster, opt => opt.Ignore());

        CreateMap<DateTime, ProposedDate>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.QuestId, opt => opt.Ignore())
            .ForMember(dest => dest.Quest, opt => opt.Ignore());

        CreateMap<Quest, QuestViewModel>()
            .ForMember(dest => dest.ProposedDates, opt => opt.MapFrom(src => src.ProposedDates.Select(pd => pd.Date).ToList()))
            .ForMember(dest => dest.DungeonMasterId, opt => opt.MapFrom(src => src.DungeonMaster != null ? src.DungeonMaster.Id : 0));

        // Shop mappings

        // ShopItem to ShopItemViewModel
        CreateMap<ShopItem, ShopItemViewModel>()
            .ForMember(dest => dest.CreatedByDmName, opt => opt.MapFrom(src => src.CreatedByDm != null ? src.CreatedByDm.Name : "Unknown"));

        // ShopItem to CreateShopItemViewModel (reverse)
        CreateMap<CreateShopItemViewModel, ShopItem>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.CreatedByDm, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByDmId, opt => opt.Ignore());

        // ShopItem to EditShopItemViewModel
        CreateMap<ShopItem, EditShopItemViewModel>();

        // EditShopItemViewModel to ShopItem
        CreateMap<EditShopItemViewModel, ShopItem>()
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByDm, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByDmId, opt => opt.Ignore());

        // ShopItem to ShopItemDetailsViewModel
        CreateMap<ShopItem, ShopItemDetailsViewModel>()
            .ForMember(dest => dest.CreatedByDmName, opt => opt.MapFrom(src => src.CreatedByDm != null ? src.CreatedByDm.Name : "Unknown"));

        // UserTransaction to UserTransactionViewModel
        CreateMap<UserTransaction, UserTransactionViewModel>()
            .ForMember(dest => dest.ItemName, opt => opt.MapFrom(src => src.ShopItem != null ? src.ShopItem.Name : "Unknown Item"));

        // Character to CharacterViewModel
        CreateMap<Character, CharacterViewModel>()
            .ForMember(dest => dest.OwnerName, opt => opt.MapFrom(src => src.Owner != null ? src.Owner.Name : "Unknown"))
            .ForMember(dest => dest.IsOwner, opt => opt.Ignore())
            .ForMember(dest => dest.ProfilePictureFile, opt => opt.Ignore());

        // CharacterViewModel to Character
        CreateMap<CharacterViewModel, Character>()
            .ForMember(dest => dest.Owner, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ProfilePicture, opt => opt.Ignore());

        // CharacterClass mappings
        CreateMap<CharacterClass, CharacterClassViewModel>()
            .ReverseMap();

        // Contact to ContactViewModel
        CreateMap<Contact, ContactViewModel>()
            .ForMember(dest => dest.HasContactImage, opt => opt.MapFrom(src => src.HasContactImage))
            .ForMember(dest => dest.ContactImageFile, opt => opt.Ignore())
            .ForMember(dest => dest.CanManage, opt => opt.Ignore());

        // ContactViewModel to Contact
        CreateMap<ContactViewModel, Contact>()
            .ForMember(dest => dest.ContactImageData, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedByUser, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Notes, opt => opt.Ignore());

        // ContactNote to ContactNoteViewModel
        CreateMap<ContactNote, ContactNoteViewModel>();

        // ContactNoteViewModel to ContactNote
        CreateMap<ContactNoteViewModel, ContactNote>()
            .ForMember(dest => dest.AuthorName, opt => opt.Ignore());

        // Quest to QuestSummaryViewModel (for DM profile quest history)
        CreateMap<Quest, QuestSummaryViewModel>()
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.FinalizedDate));

        // Event to EventViewModel
        CreateMap<Event, EventViewModel>()
            .ForMember(dest => dest.CanManage, opt => opt.Ignore())
            // Roster, IsOneShotBoard, HasOwnSignup and MyAvailability are all computed
            // server-side per request, exactly like CanManage above.
            .ForMember(dest => dest.Roster, opt => opt.Ignore())
            .ForMember(dest => dest.IsOneShotBoard, opt => opt.Ignore())
            .ForMember(dest => dest.HasOwnSignup, opt => opt.Ignore())
            .ForMember(dest => dest.MyAvailability, opt => opt.Ignore())
            // The recurrence form inputs and the save-scope field are never read back off a
            // domain model -- they only ever flow from a submitted form into a write action.
            // SeriesId and CancelledAt map by convention and must not be ignored: the details,
            // calendar and edit surfaces all read them.
            .ForMember(dest => dest.IsRecurring, opt => opt.Ignore())
            .ForMember(dest => dest.IntervalWeeks, opt => opt.Ignore())
            .ForMember(dest => dest.CycleMask, opt => opt.Ignore())
            .ForMember(dest => dest.SeriesEndDate, opt => opt.Ignore())
            .ForMember(dest => dest.EditScope, opt => opt.Ignore());

        // EventSignup to EventSignupViewModel -- property names line up so no member
        // configuration is needed. There is no reverse map: the roster is read-only, and the
        // write actions take primitive route/form values rather than a bound model.
        CreateMap<EventSignup, EventSignupViewModel>();

        // EventViewModel to Event
        // GroupId, SeriesId, SeriesSlotIndex, CreatedAt and CancelledAt are set server-side and
        // are never taken from a submitted form, because a hidden field is not a security
        // boundary -- without ignoring CancelledAt here, a crafted post through the ordinary
        // edit path could clear or set a cancellation.
        CreateMap<EventViewModel, Event>()
            .ForMember(dest => dest.GroupId, opt => opt.Ignore())
            .ForMember(dest => dest.SeriesId, opt => opt.Ignore())
            .ForMember(dest => dest.SeriesSlotIndex, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CancelledAt, opt => opt.Ignore());

        // EventSeries to SeriesDetailsViewModel -- deliberately no reverse map: the series page
        // is read-only and its two write actions take primitive route and form values rather
        // than a bound model. CyclePositions, Occurrences, CanManage and the removal-impact
        // counts are all filled per request by the controller, not from the domain row.
        CreateMap<EventSeries, SeriesDetailsViewModel>()
            .ForMember(dest => dest.CyclePositions, opt => opt.Ignore())
            .ForMember(dest => dest.Occurrences, opt => opt.Ignore())
            .ForMember(dest => dest.CanManage, opt => opt.Ignore())
            .ForMember(dest => dest.PastCount, opt => opt.Ignore())
            .ForMember(dest => dest.FutureCount, opt => opt.Ignore())
            .ForMember(dest => dest.AnsweredCount, opt => opt.Ignore());

        // Event to SeriesOccurrenceViewModel -- maps by convention, including IsCancelled,
        // which is computed on the source model from CancelledAt.
        CreateMap<Event, SeriesOccurrenceViewModel>();

        // AvailabilityMember to OverviewMemberViewModel -- property names line up so no
        // member configuration is needed.
        CreateMap<AvailabilityMember, OverviewMemberViewModel>();

        // EventAvailabilityRow to EventOverviewRowViewModel -- deliberately no reverse map:
        // the availability overview page is read-only and takes no bound model, matching
        // the EventSignup/EventSeries precedent above. There is no EventOverviewViewModel
        // map either; the controller assembles the container directly, exactly as
        // CalendarController assembles CalendarViewModel by hand.
        CreateMap<EventAvailabilityRow, EventOverviewRowViewModel>()
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event.Id))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Event.Title))
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Event.Date))
            .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Event.StartTime));

        // AgendaRosterEntry to AgendaRosterEntryViewModel -- property names line up so no
        // member configuration is needed.
        CreateMap<AgendaRosterEntry, AgendaRosterEntryViewModel>();

        // AgendaRow to AgendaRowViewModel -- deliberately no reverse map: the agenda page is
        // read-only and takes no bound model, matching the availability overview precedent
        // above. There is no AgendaViewModel container map either; the controller assembles
        // it directly, exactly as the overview and calendar containers already do.
        // BoardName, BoardType and IsActiveBoard are explicitly ignored here rather than left
        // to convention -- none of the three exists on the source row, and they are set by
        // the controller after mapping from its own membership read.
        CreateMap<AgendaRow, AgendaRowViewModel>()
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event.Id))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Event.Title))
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Event.Date))
            .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Event.StartTime))
            .ForMember(dest => dest.BoardId, opt => opt.MapFrom(src => src.Event.GroupId))
            .ForMember(dest => dest.BoardName, opt => opt.Ignore())
            .ForMember(dest => dest.BoardType, opt => opt.Ignore())
            .ForMember(dest => dest.IsActiveBoard, opt => opt.Ignore());
    }
}