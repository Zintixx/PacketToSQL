namespace PacketToSQL.Types;

public class Shop
{
    public  int Id;
    public int CategoryId;
    public string Name;
    public ShopType Skin;
    public byte HideUnuseable;
    public byte HideStats;
    public byte DisableBuyback;
    public byte OpenWallet;
    public byte DisplayNew;
    public byte RandomizeOrder;
    public byte CanRestock;
    public ShopRestockData? RestockData;
}

public class ShopItem
{
    public int Uid;
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ItemId { get; set; }
    public ShopCurrencyType CurrencyType { get; set; }
    public int CurrencyItemId { get; set; }
    public int Price { get; set; }
    public int SalePrice { get; set; }
    public byte Rarity { get; set; }
    public int StockCount { get; set; }
    public string Category { get; set; }
    public int RequireGuildTrophy { get; set; }
    public int RequireAchievementId { get; set; }
    public int RequireAchievementRank { get; set; }
    public byte RequireChampionshipGrade { get; set; }
    public short RequireChampionshipJoinCount { get; set; }
    public byte RequireGuildMerchantType { get; set; }
    public short RequireGuildMerchantLevel { get; set; }
    public short Quantity { get; set; }
    public ShopItemLabel Label { get; set; }
    public string CurrencyIdString { get; set; }
    public short RequireQuestAllianceId { get; set; }
    public int RequireFameGrade { get; set; }
    public byte AutoPreviewEquip { get; set; }
}

public class ShopRestockData {

    public ShopRestockInterval Interval { get; init; }
    public ShopCurrencyType CurrencyType { get; init; }
    public ShopCurrencyType ExcessCurrencyType { get; init; }
    public int Cost { get; init; }
    public bool EnableCostMultiplier { get; init; }
    public int RestockCount { get; set; }
    public bool DisableInstantRestock { get; init; }
    public bool PersistantInventory { get; init; }

}

public class ShopCost {
    public ShopCurrencyType Type { get; init; }
    public int ItemId { get; init; }
    public int Amount { get; init; }
    public int SaleAmount { get; init; }
}

public enum ShopRestockInterval : byte {
    Minute = 0,
    Day = 1,
    Week = 2,
    Month = 3,
}

public enum ShopCurrencyType : byte
{
    Meso = 0,
    Item = 1,
    ValorToken = 2,
    Treva = 3,
    Meret = 4,
    Rue = 5,
    HaviFruit = 6,
    GuildCoin = 7,
    ReverseCoin = 8,
    EventMeret = 9,
    GameMeret = 10,
    MentorPoints = 11,
    MenteePoints = 12,
    EventToken = 13
}

public enum ShopType : byte
{
    Default = 0,
    Unk = 1,
    Star = 2,
    StyleCrate = 3,
    Capsule = 4
}

public enum ShopItemLabel : byte
{
    None = 0,
    New = 1,
    Event = 2,
    HalfPrice = 3,
    Special = 4
}
