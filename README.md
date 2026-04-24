# PrisonerBlood

**PrisonerBlood** is a server-side V Rising mod that allows players to purchase 100% blood quality prisoners into nearby empty prison cells, buy 100% Blood Merlot potions directly into their inventory, and sell existing prisoners for currency based on their blood quality.

## What's New in v1.0.1
- **Sell Your Prisoners:** Added a prisoner selling system.
    - The selling price scales with the prisoner's blood quality.
    - Example 1: 100% quality = 100% of Blood Prices = 2500
    - Example 2: 86% quality = 86% of Blood Prices = 2150

- **Bulk Buy Potions:** Added the ability to buy multiple Blood potions in a single command.
    - Example 1: `.buy bp rogue` = buy 1 Blood potion.
    - Example 2: `.buy bp rogue 5` = buy 5 Blood potions.

## Features
- **Buy Prisoners:** Spawn a 100% blood quality prisoner directly into the nearest empty prison cell you own.  
- **Buy Blood Potions:** Add 100% Blood Merlot potions directly to your inventory.  
- **Sell Prisoners:** Sell prisoners from your prison cells for currency. The price scales with their blood quality.  
- **Customizable Configuration:** Easily configure currencies, base prices, blood type prices, and minimum sellable quality via JSON files.  
- **Logging System:** Record all purchases and sales in separate CSV log files.

## Requirements
1. [BepInEx 1.733.2](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/)
2. [VampireCommandFramework 0.11.0](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)

## Installation
1. Install the required dependencies.
2. Place `PrisonerBlood.dll` into your server's BepInEx plugins folder.
3. Start the server once to generate the config files.
4. Edit the config files as needed.
5. Restart the server or use the reload command.

## Commands

### Player Commands
- `.buy prisoner <bloodtype>`
  - Buy a prisoner with 100% blood quality.
  - Shortcut: *.buy ps <bloodtype>*
  - Example: *.buy prisoner rogue*

- `.buy bloodpotion <bloodtype> [amount]`
  - Buy one or multiple Blood Merlot potions with 100% blood quality.
  - Shortcut: *.buy bp <bloodtype> [amount]*
  - Example: *.buy bp scholar 1* or *.buy bp scholar*

- `.sell prisoner`
  - Sell the prisoner currently in your nearest occupied prison cell.
  - Shortcut: *.sell ps*

- `.buy prisoner help`
  - Show available blood types, prices, and usage for prisoners.
  - Shortcut: *.buy ps help*

- `.buy bloodpotion help`
  - Show available blood types, prices, and usage for blood potions.
  - Shortcut: *.buy bp help*

- `.sell prisoner help`
  - Show available blood types, selling price formulas, and quality requirements.
  - Shortcut: *.sell ps help*

> **Available Blood Types:** `worker`, `creature`, `mutant`, `draculin`, `corrupted`, `rogue`, `warrior`, `brute`, `scholar`

### Admin Commands
- `.buy reload`
  - Reload all configurations (`buyconfig.json` and `sellconfig.json`).
  - Shortcut: *.buy rl*

- `.sell reload`
  - Reload all configurations (`sellconfig.json` and `buyconfig.json`).
  - Shortcut: *.sell rl*

## Config Files
After the first server start, the following files will be created:
- `BepInEx/config/PrisonerBlood/buyconfig.json`
- `BepInEx/config/PrisonerBlood/sellconfig.json`
- `BepInEx/config/PrisonerBlood/buyprisoner_log.csv`
- `BepInEx/config/PrisonerBlood/buybloodpotion_log.csv`
- `BepInEx/config/PrisonerBlood/sellprisoner_log.csv`

### buyconfig.json
- `Enabled`: (true/false) Enable or disable specific features.
- `CurrencyPrefab`: Defines the item used as currency.
- `CurrencyName`: Display name shown to players.
- `DefaultCost`: The base price for purchasing.
- `BloodCosts`: Overrides prices for specific blood types.
- Removing a blood type from `BloodCosts` does NOT disable it. The system will use `DefaultCost` instead.

```json
{
  "Prisoner": {
    "Enabled": true,
    "CurrencyPrefab": 576389135,
    "CurrencyName": "Greater Stygian Shards",
    "DefaultCost": 5000,
    "BloodCosts": {
      "Worker": 4000,
      "Creature": 4200,
      "Mutant": 4500,
      "Corrupted": 4800,
      "Draculin": 5000,
      "Warrior": 5200,
      "Rogue": 5500,
      "Brute": 5700,
      "Scholar": 6000
    }
  },
  "BloodPotion": {
    "Enabled": true,
    "CurrencyPrefab": 576389135,
    "CurrencyName": "Greater Stygian Shards",
    "DefaultCost": 500,
    "BloodCosts": {
      "Worker": 300,
      "Creature": 350,
      "Mutant": 400,
      "Corrupted": 450,
      "Draculin": 500,
      "Warrior": 550,
      "Rogue": 600,
      "Brute": 650,
      "Scholar": 700
    }
  }
}
```

### sellconfig.json
- `Enabled`: (true/false) Enable or disable specific features.
- `MinSellableQuality`: Minimum blood quality required to sell a prisoner.
- `CurrencyPrefab`: Defines the item used as currency.
- `CurrencyName`: Display name shown to players.
- `DefaultPrice`: The base price for all blood types.
- `BloodPrices`: Overrides prices for specific blood types.
- Removing a blood type from `BloodPrices` does NOT disable it. The system will use `DefaultPrice` instead.

```json
{
  "Prisoner": {
    "Enabled": true,
    "MinSellableQuality": 80,
    "CurrencyPrefab": 576389135,
    "CurrencyName": "Greater Stygian Shards",
    "DefaultPrice": 2500,
    "BloodPrices": {
      "Worker": 2000,
      "Creature": 2100,
      "Mutant": 2250,
      "Corrupted": 2400,
      "Draculin": 2500,
      "Warrior": 2600,
      "Rogue": 2750,
      "Brute": 2850,
      "Scholar": 3000
    }
  }
}
```

## Credits
- [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) by **odjit** for the original code that inspired this mod.
- [PrisonerExchange](https://thunderstore.io/c/v-rising/p/helskog/PrisonerExchange/) by **helskog** for the original code that inspired the buy prisoner system.
- [KindredSacrifice](https://thunderstore.io/c/v-rising/p/odjit/KindredSacrifice/) by **odjit** for the original code that inspired the sell prisoner system.
- [V Rising modding community](https://discord.com/invite/QG2FmueAG9)

## License
This project is licensed under the AGPL-3.0 license.

## Notes
> - This mod was first made for my own server and originally ran through KindredCommands. It has now been separated into a standalone mod so that everyone can use it.
> - If you have any problems or run into bugs, please report them to me in the [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9)
> **Del** (delta_663)
