using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository.Automapper;

public class EntityProfile : Profile
{
    public EntityProfile()
    {
        // Quest mapping
        // OriginalQuest/FollowUpQuest are mapped shallowly (Id + Title only) to avoid
        // circular AutoMapper recursion. The Quest → QuestEntity direction still ignores
        // them since EF handles the FK (OriginalQuestId) directly.
        CreateMap<QuestEntity, Quest>()
            .ForMember(dest => dest.OriginalQuest, opt => opt.MapFrom(src =>
                src.OriginalQuest == null
                    ? null
                    : new Quest { Id = src.OriginalQuest.Id, Title = src.OriginalQuest.Title }))
            .ForMember(dest => dest.FollowUpQuest, opt => opt.MapFrom(src =>
                src.FollowUpQuest == null
                    ? null
                    : new Quest { Id = src.FollowUpQuest.Id, Title = src.FollowUpQuest.Title }));

        CreateMap<Quest, QuestEntity>()
            .ForMember(dest => dest.OriginalQuest, opt => opt.Ignore())
            .ForMember(dest => dest.FollowUpQuest, opt => opt.Ignore());

        // User mapping
        CreateMap<User, UserEntity>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore()) // Password is handled by Identity
            .ForMember(dest => dest.SecurityStamp, opt => opt.Ignore())
            .ForMember(dest => dest.ConcurrencyStamp, opt => opt.Ignore());

        CreateMap<UserEntity, User>();

        // PlayerSignup mapping
        CreateMap<PlayerSignup, PlayerSignupEntity>()
            .ForMember(dest => dest.Quest, opt => opt.Ignore())
            .ForMember(dest => dest.QuestId, opt => opt.MapFrom(src => src.Quest.Id))
            .ForMember(dest => dest.Player, opt => opt.Ignore())
            .ForMember(dest => dest.PlayerId, opt => opt.MapFrom(src => src.Player.Id))
            .ForMember(dest => dest.SignupRole, opt => opt.MapFrom(src => (int)src.Role));

        CreateMap<PlayerSignupEntity, PlayerSignup>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (SignupRole)src.SignupRole));

        // ProposedDate mapping
        CreateMap<ProposedDate, ProposedDateEntity>()
            .ReverseMap();

        // ReminderLog mapping
        CreateMap<ReminderLog, ReminderLogEntity>().ReverseMap();

        // PlayerDateVote mapping
        CreateMap<PlayerDateVote, PlayerDateVoteEntity>()
            .ForMember(dest => dest.Vote, opt => opt.MapFrom(src => src.Vote.HasValue ? (int)src.Vote.Value : (int?)null));

        CreateMap<PlayerDateVoteEntity, PlayerDateVote>()
            .ForMember(dest => dest.Vote, opt => opt.MapFrom(src => src.Vote.HasValue ? (VoteType)src.Vote.Value : (VoteType?)null));

        // Shop entity mappings

        // ShopItem mapping with enum conversions
        CreateMap<ShopItem, ShopItemEntity>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => (int)src.Type))
            .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => (int)src.Rarity))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status));

        CreateMap<ShopItemEntity, ShopItem>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => (ItemType)src.Type))
            .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => (ItemRarity)src.Rarity))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (ItemStatus)src.Status));


        // UserTransaction mapping
        CreateMap<UserTransaction, UserTransactionEntity>()
            .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => (int)src.TransactionType));

        CreateMap<UserTransactionEntity, UserTransaction>()
            .ForMember(dest => dest.TransactionType, opt => opt.MapFrom(src => (TransactionType)src.TransactionType));

        // Character mapping
        CreateMap<Character, CharacterEntity>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (int)src.Role))
            .ForMember(dest => dest.ProfileImage, opt => opt.MapFrom(src => src.ProfilePicture == null
                ? null
                : new CharacterImageEntity
                {
                    OriginalImageData = src.ProfilePicture
                }));

        CreateMap<CharacterEntity, Character>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (CharacterStatus)src.Status))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => (CharacterRole)src.Role))
            .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src => src.ProfileImage != null
                ? src.ProfileImage.OriginalImageData
                : null));

        // CharacterClass mapping with enum conversions
        CreateMap<CharacterClass, CharacterClassEntity>()
            .ForMember(dest => dest.Class, opt => opt.MapFrom(src => (int)src.Class));

        CreateMap<CharacterClassEntity, CharacterClass>()
            .ForMember(dest => dest.Class, opt => opt.MapFrom(src => (DndClass)src.Class));

        // Contact mapping
        CreateMap<Contact, ContactEntity>()
            .ForMember(dest => dest.ProfileImage, opt => opt.MapFrom(src => src.ContactImageData == null
                ? null
                : new ContactImageEntity
                {
                    OriginalImageData = src.ContactImageData
                }))
            .ForMember(dest => dest.Notes, opt => opt.Ignore());

        CreateMap<ContactEntity, Contact>()
            .ForMember(dest => dest.ContactImageData, opt => opt.MapFrom(src => src.ProfileImage != null
                ? src.ProfileImage.OriginalImageData
                : null));

        // ContactNote mapping — AuthorName is a display-only projection from the Author navigation
        CreateMap<ContactNote, ContactNoteEntity>()
            .ForMember(dest => dest.Author, opt => opt.Ignore());

        CreateMap<ContactNoteEntity, ContactNote>()
            .ForMember(dest => dest.AuthorName, opt => opt.MapFrom(src => src.Author != null ? src.Author.Name : null));

        // DungeonMasterProfile mappings
        CreateMap<DungeonMasterProfileEntity, DungeonMasterProfile>()
            .ForMember(dest => dest.ProfilePicture, opt => opt.MapFrom(src =>
                src.ProfileImage != null ? src.ProfileImage.OriginalImageData : null));

        CreateMap<DungeonMasterProfile, DungeonMasterProfileEntity>()
            .ForMember(dest => dest.ProfileImage, opt => opt.Ignore());

        // Group mapping with BoardType int<->enum conversion
        CreateMap<GroupEntity, Group>()
            .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (BoardType)src.BoardType));

        CreateMap<Group, GroupEntity>()
            .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (int)src.BoardType));

        // UserGroup mapping with GroupRole int↔enum conversion
        CreateMap<UserGroupEntity, UserGroup>()
            .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (GroupRole)src.GroupRole))
            .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User));

        CreateMap<UserGroup, UserGroupEntity>()
            .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (int)src.GroupRole))
            .ForMember(dest => dest.User, opt => opt.Ignore());
    }
}
