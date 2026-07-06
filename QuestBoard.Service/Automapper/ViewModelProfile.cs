using AutoMapper;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Service.ViewModels.QuestViewModels;
using QuestBoard.Service.ViewModels.ShopViewModels;
using QuestBoard.Service.ViewModels.CharacterViewModels;
using QuestBoard.Service.ViewModels.ContactViewModels;
using QuestBoard.Service.ViewModels.DungeonMasterViewModels;

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
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());

        // CharacterClass mappings
        CreateMap<CharacterClass, CharacterClassViewModel>()
            .ReverseMap();

        // Contact to ContactViewModel
        CreateMap<Contact, ContactViewModel>()
            .ForMember(dest => dest.ContactImageFile, opt => opt.Ignore())
            .ForMember(dest => dest.CanManage, opt => opt.Ignore());

        // ContactViewModel to Contact
        CreateMap<ContactViewModel, Contact>()
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
    }
}