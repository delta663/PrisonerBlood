# PrisonerBlood

PrisonerBlood is a **server-side** V Rising mod that lets players buy **Prisoners with 100% blood quality** directly into nearby empty prison cells, as well as **100% Blood Merlot potions** directly into their inventory.

## Features
- Buy prisoners with 100% blood quality using in-game commands.
- Spawns a prisoner directly into the nearest empty prison cell you own.
- Buy Blood Merlot potions with 100% blood quality using in-game commands.
- Adds a blood potion directly to the buyer's inventory.
- Uses a simple JSON config file for currency and pricing.
- Logs prisoner and blood potion purchases to separate CSV files.
- Includes an admin command to reload the config.

## Requirements
1. [BepInEx 1.733.2](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/)
2. [VampireCommandFramework 0.10.4](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)

## Installation
1. Install the required dependencies.
2. Place `PrisonerBlood.dll` into your server's BepInEx plugins folder.
3. Start the server once to generate the config files.
4. Edit the config file as needed.
5. Restart the server or use the reload command.

## Commands

### Player Commands
- `.buy prisoner <bloodtype>`
  - Buy a prisoner with 100% blood quality.
  - Shortcut: *.buy ps <bloodtype>*
  - Example: *.buy prisoner rogue*
  - Blood Types: `worker`, `creature`, `mutant`, `draculin`, `corrupted`, `rogue`, `warrior`, `brute`, `scholar`

- `.buy bloodpotion <bloodtype>`
  - Buy a Blood Merlot potion with 100% blood quality.
  - Shortcut: *.buy bp <bloodtype>*
  - Example: *.buy bloodpotion scholar*
  - Blood Types: `worker`, `creature`, `mutant`, `draculin`, `corrupted`, `rogue`, `warrior`, `brute`, `scholar`

- `.buy prisoner help`
  - Show available blood types, prices, and usage for prisoners.
  - Shortcut: *.buy ps help*

- `.buy bloodpotion help`
  - Show available blood types, prices, and usage for blood potions.
  - Shortcut: *.buy bp help*

### Admin Commands
- `.buy reload`
  - Reload `buyconfig.json`.
  - Shortcut: *.buy rl*

## Config Files

After the first server start, the following files will be created:
- `BepInEx/config/PrisonerBlood/buyconfig.json`
- `BepInEx/config/PrisonerBlood/buyprisoner_log.csv`
- `BepInEx/config/PrisonerBlood/buybloodpotion_log.csv`

### buyconfig.json
- Each section (`Prisoner`, `BloodPotion`) can be enabled or disabled via `Enabled` (true/false)
- `CurrencyPrefab` defines the item used as currency.
- `CurrencyName` is the display name shown to players.
- `DefaultCost` sets the base price for all blood types.
- `BloodCosts` allows overriding prices for specific blood types.
- If a blood type is not listed in `BloodCosts`, it will still be available for purchase and will use `DefaultCost`. Removing a blood type from this list does NOT disable it.

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

## Credits
- [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) by **odjit** for the original code that inspired this mod.
- [PrisonerExchange](https://thunderstore.io/c/v-rising/p/helskog/PrisonerExchange/) by **helskog** for the original code that inspired the prisoner system.
- **V Rising modding community**

## License
This project is licensed under the AGPL-3.0 license.

## Notes
> - This mod was first made for my own server and originally ran through KindredCommands. It has now been separated into a standalone mod so that everyone can use it.
> - If you have any problems or run into bugs, please report them to me in the [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9)
> **Del** (delta_663)
