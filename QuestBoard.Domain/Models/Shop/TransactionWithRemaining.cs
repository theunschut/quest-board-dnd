namespace QuestBoard.Domain.Models.Shop;

public record TransactionWithRemaining(UserTransaction Transaction, int RemainingQuantity);
