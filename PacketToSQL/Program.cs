using System.Text.Json;
using PacketToSQL.Helpers;
using PacketToSQL.MapleShark2_Files;
using PacketToSQL.Types;

Console.WriteLine("Welcome to the MapleShark2 sniff to MapleServer 2 Shop SQL.");

Begin:

string appPath = Path.GetFullPath(AppContext.BaseDirectory);
Console.WriteLine($"Enter the folder path: (leave blank to use current folder '{appPath}')");

string path = Console.ReadLine();
if (string.IsNullOrEmpty(path))
{
    path = appPath;
}

if (!Directory.Exists(path))
{
    Console.WriteLine("Could not find path. Press Y to try again or N to leave");
    ConsoleKeyInfo key = Console.ReadKey();
    if (key.Key is ConsoleKey.Y)
    {
        goto Begin;
    }

    return;
}

string[] files = Directory.GetFiles(path, "*.msb", SearchOption.AllDirectories);
if (files.Length == 0)
{
    Console.WriteLine("No .msb files found. Press any key to close...");
    Console.ReadKey();
    return;
}

Console.WriteLine($"Found {files.Length} .msb files. Starting conversion now...");

foreach (string file in files)
{
    (MsbMetadata metadata, IEnumerable<MaplePacket> packets) = FileLoader.ReadMsbFile(file);

    bool isGms2 = metadata.Build == 12;
    int opCode = isGms2 ? 82 : 81;
    Dictionary<int, (Shop, List<ShopItem>)> shops = new();

    int lastShopId = 0;
    foreach (MaplePacket packet in packets.Where(x => x.Opcode == opCode && !x.Outbound))
    {
        byte mode = packet.ReadByte();
        switch (mode)
        {
            case 0:
                Shop shop = ReadShop(packet);
                lastShopId = shop.Id;
                if (!shops.ContainsKey(lastShopId)) {
                    shops[lastShopId] = (shop, new());
                }
                break;
            case 1:
                if (lastShopId == -1)
                {
                    Console.WriteLine("Error: Tried to read shop item without reading shop first");
                    continue;
                }

                if (isGms2)
                {
                    ShopItem item = ReadShopItemGms2(packet);
                    shops[lastShopId].Item2.Add(item);
                    break;
                }

                List<ShopItem> items = ReadShopItemKms2(packet);
                foreach (ShopItem item in items) {
                    if (shops[lastShopId].Item2.Any(x => x.ItemId == item.ItemId && x.Rarity == item.Rarity))
                    {
                        continue;
                    }
                    shops[lastShopId].Item2.Add(item);
                }

                break;
            case 6:
                lastShopId = -1;
                break;
        }
    }

    if (shops.Count == 0)
    {
        continue;
    }

    foreach ((int _, (Shop shop, List<ShopItem> shopItems)) in shops)
    {
        CreateShopSqlFile(shop);
        CreateShopItemsSqlFile(shopItems, shop.Id);
    }

    Console.Write(".");
}

Console.WriteLine();
Console.WriteLine("Done. Press any key to close...");
Console.ReadKey();

Shop ReadShop(MaplePacket packet)
{
    packet.ReadInt();
    int id = packet.ReadInt();
    long nextRestock = packet.ReadLong();
    packet.ReadInt();
    int itemCount = packet.ReadShort();
    int category = packet.ReadInt();
    byte openWallet = packet.ReadByte();
    byte disableBuyBack = packet.ReadByte();
    byte canRestock = packet.ReadByte();
    byte randomizeOrder = packet.ReadByte();
    ShopType shopType = (ShopType) packet.ReadByte();
    byte hideUnuseable = packet.ReadByte();
    byte hideStats = packet.ReadByte();
    packet.ReadBool();
    byte displayNew = packet.ReadByte();
    string name = packet.ReadString();

    ShopRestockData data = null;
    if (canRestock == 1)
    {

        ShopCurrencyType restockCurrencyType = (ShopCurrencyType) packet.ReadByte();
        ShopCurrencyType excessRestockCurrencyType = (ShopCurrencyType) packet.ReadByte();
        packet.ReadInt();
        int restockCost = packet.ReadInt();
        bool enableRestockCostMultiplier = packet.ReadBool();
        int totalRestockCount = packet.ReadInt();
        ShopRestockInterval restockInterval = (ShopRestockInterval) packet.ReadByte();
        bool disableInstantRestock = packet.ReadBool();
        bool persistantInventory = packet.ReadBool();
        data = new ShopRestockData() {
            CurrencyType = restockCurrencyType,
            ExcessCurrencyType = excessRestockCurrencyType,
            Cost = restockCost,
            EnableCostMultiplier = enableRestockCostMultiplier,
            RestockCount = totalRestockCount,
            Interval = restockInterval,
            DisableInstantRestock = disableInstantRestock,
            PersistantInventory = persistantInventory,
        };

    }

    return new()
    {
        HideUnuseable = hideUnuseable,
        CanRestock = canRestock,
        CategoryId = category,
        Id = id,
        Name = name,
        DisableBuyback = disableBuyBack,
        Skin = shopType,
        OpenWallet = openWallet,
        RandomizeOrder = randomizeOrder,
        HideStats = hideStats,
        DisplayNew = displayNew,
        RestockData = data,
    };
}

ShopItem ReadShopItemGms2(MaplePacket packet)
{
    packet.ReadByte(); // count
    return ReadItemShop(packet);
}

List<ShopItem> ReadShopItemKms2(MaplePacket packet)
{
    List<ShopItem> shopItems = new();
    byte count = packet.ReadByte();
    for (int i = 0; i < count; i++)
    {
        ShopItem item = ReadItemShop(packet);
        packet.ReadByte();
        packet.ReadKmsItem(item.ItemId);

        shopItems.Add(item);
    }


    return shopItems;
}

async void CreateShopSqlFile(Shop shop)
{
    string sqlFolder = Path.Combine(appPath, "SQL Files");
    Directory.CreateDirectory(sqlFolder);
    string restockData = JsonSerializer.Serialize(shop.RestockData);

    string shopsFilePath = Path.Combine(sqlFolder, "Shop.sql");

    if (!File.Exists(shopsFilePath))
    {
        File.WriteAllLines(shopsFilePath, new[]
        {
            "-- SQL File created by PacketToSQL app by tDcc#0568", "INSERT INTO `game-server`.`shop` (`Id`,`CategoryId`, `Name`, `Skin`, `HideUnuseable`,"
                                                                   + "`HideStats`, `DisableBuyback`, `OpenWallet`, `DisplayNew`, `RandomizeOrder`, `RestockData`)"
                                                                   + "VALUES"
        });
    }

    await using StreamWriter file = new(shopsFilePath, append: true);

    string[] lines =
    {
        $"({shop.Id}, {shop.CategoryId}, '{shop.Name}', {(byte) shop.Skin}, {shop.HideUnuseable}, {shop.HideStats}, " +
        $"{shop.DisableBuyback}, {shop.OpenWallet}, {shop.DisplayNew}, {shop.RandomizeOrder}, " +
        $"'{restockData}')," +
        "" // new line
    };

    foreach (string line in lines)
    {
        await file.WriteLineAsync(line);
    }
}

async void CreateShopItemsSqlFile(List<ShopItem> items, int shopId)
{
    string sqlFolder = Path.Combine(appPath, "SQL Files");
    Directory.CreateDirectory(sqlFolder);

    string shopsItemsFilePath = Path.Combine(sqlFolder, "ShopsItem.sql");

    if (!File.Exists(shopsItemsFilePath))
    {
        File.WriteAllLines(shopsItemsFilePath, new[]
        {
            "-- SQL File created by PacketToSQL app by tDcc#0568",
            @"INSERT INTO `game-server`.`shop-item` (`ShopId`, `ItemId`, `CurrencyType`, `CurrencyItemId`, `Price`, `SalePrice`,
                            `Rarity`, `StockCount`, `Category`,
                            `RequireGuildTrophy`, `RequireAchievementId`, `RequireAchievementRank`,
                            `RequireChampionshipGrade`, `RequireChampionshipJoinCount`, `RequireGuildMerchantType`,
                            `RequireGuildMerchantLevel`, `Quantity`, `Label`, `CurrencyIdString`, `RequireQuestAllianceId`, `RequireFameGrade`, `AutoPreviewEquip`)
VALUES"
        });
    }

    await using StreamWriter file = new(shopsItemsFilePath, append: true);

    List<string> lines = new()
    {
        $"-- Shop ID {shopId}",
    };

    foreach (ShopItem shopItem in items)
    {
        lines.Add($"({shopId}, {shopItem.ItemId}, {(byte) shopItem.CurrencyType}, {shopItem.CurrencyItemId}, {shopItem.Price}, " +
                  $"{shopItem.SalePrice}, {shopItem.Rarity}, {shopItem.StockCount}, '{shopItem.Category}', {shopItem.RequireGuildTrophy}, " +
                  $"{shopItem.RequireAchievementId}, {shopItem.RequireAchievementRank}, {shopItem.RequireChampionshipGrade}, " +
                  $"{shopItem.RequireChampionshipJoinCount}, {shopItem.RequireGuildMerchantType}, {shopItem.RequireGuildMerchantLevel}, {shopItem.Quantity}, " +
                  $"{(byte) shopItem.Label}, '{shopItem.CurrencyIdString}', {shopItem.RequireQuestAllianceId}, {shopItem.RequireFameGrade}, {shopItem.AutoPreviewEquip}),");
    }

    lines.Add("");
    lines.Add("");

    foreach (string line in lines)
    {
        await file.WriteLineAsync(line);
    }
}

ShopItem ReadItemShop(MaplePacket maplePacket)
{
    maplePacket.ReadInt();
    int itemId = maplePacket.ReadInt();
    byte tokenType = maplePacket.ReadByte();
    int requiredItemId = maplePacket.ReadInt();
    maplePacket.ReadInt();
    int price = maplePacket.ReadInt();
    int salePrice = maplePacket.ReadInt();
    byte itemRank = maplePacket.ReadByte();
    maplePacket.ReadInt();
    int stockCount = maplePacket.ReadInt();
    int stockPurchased = maplePacket.ReadInt();
    int guildTrophy = maplePacket.ReadInt();
    string category = maplePacket.ReadString();
    int requiredAchievementId = maplePacket.ReadInt();
    int requiredAchievementGrade = maplePacket.ReadInt();
    byte requiredChampionshipGrade = maplePacket.ReadByte();
    short requiredChampionshipJoinCount = maplePacket.ReadShort();
    byte requiredGuildMerchantType = maplePacket.ReadByte();
    short requiredGuildMerchantLevel = maplePacket.ReadShort();
    maplePacket.ReadBool();
    short quantity = maplePacket.ReadShort();
    maplePacket.ReadByte();
    byte flag = maplePacket.ReadByte();
    string currencyId = maplePacket.ReadString();
    short requiredQuestAlliance = maplePacket.ReadShort();
    int requiredFameGrade = maplePacket.ReadInt();
    byte autoPreviewEquip = maplePacket.ReadByte();
    maplePacket.ReadByte();
    return new()
    {
        ItemId = itemId,
        CurrencyType = (ShopCurrencyType) tokenType,
        CurrencyItemId = requiredItemId,
        Price = price,
        SalePrice = salePrice,
        Rarity = itemRank,
        StockCount = stockCount,
        Category = category,
        RequireGuildTrophy = guildTrophy,
        RequireAchievementId = requiredAchievementId,
        RequireAchievementRank = requiredAchievementGrade,
        RequireChampionshipGrade = requiredChampionshipGrade,
        RequireChampionshipJoinCount = requiredChampionshipJoinCount,
        RequireGuildMerchantType = requiredGuildMerchantType,
        RequireGuildMerchantLevel = requiredGuildMerchantLevel,
        Quantity = quantity,
        Label = (ShopItemLabel) flag,
        CurrencyIdString = currencyId,
        RequireQuestAllianceId = requiredQuestAlliance,
        RequireFameGrade = requiredFameGrade,
        AutoPreviewEquip = autoPreviewEquip,
    };
}
