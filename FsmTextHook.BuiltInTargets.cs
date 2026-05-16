using System.Collections.Generic;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Hardcoded FSM target registrations for <see cref="FsmTextHook"/>.
    /// Split out as a partial class purely for readability - the registration
    /// table dwarfs the rest of the hook and obscures its actual logic when
    /// inlined.
    /// </summary>
    public partial class FsmTextHook
    {
        private void AddBuiltInTargets(Dictionary<string, FsmTarget> byKey)
        {
            // Radio (MainMenu)
            AddTargetRule(byKey, "Radio/Folk", "LoadSongs", "", -1, "NOT IMPORTED");
            AddTargetRule(byKey, "Radio/Folk", "LoadSongs", "Off", 1, "RADIO IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "Playlist", "", -1, "NOT IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "Playlist", "State 1", 0, "CD'S IMPORTED");

            // TV Teletext pages (240/241/302 results, Teletext command bar)
            AddTargetRule(byKey, "Systems/Teletext", "Pages", "Reset page", 4, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "Pages", "Reset page", 4, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "Pages", "Load", 0, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "Pages", "Load", 0, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "Pages", "Open page", 0, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "Pages", "Open page", 0, "haku");

            // TV Forecast: 188
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "Logic", "State 3", 0, "PILVIST\u00C4");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "Logic", "State 4", 0, "VESISADETTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "Logic", "State 5", 0, "UKKOSTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "Logic", "State 6", 0, "SELKE\u00C4\u00C4");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "Logic", "State 3", 0, "PILVIST\u00C4");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "Logic", "State 4", 0, "VESISADETTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "Logic", "State 5", 0, "UKKOSTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "Logic", "State 6", 0, "SELKE\u00C4\u00C4");

            // Sheets: Rally results / registration / penalties
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "State 1", 0, "Junior Cup");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "State 1", 0, " - Class points");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 6, "Time penalty:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 6, "sec.");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 7, "Parc Ferme violation:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 8, "Jump start violation:");
            // Rally class name source values (SetStringValue.stringValue) the sheets read back from
            AddTargetRule(byKey, "RACES/RALLY/ResultsWeekend", "Data", "State 5", 2, "Amateur");
            AddTargetRule(byKey, "RACES/RALLY/ResultsWeekend", "Data", "State 6", 2, "Junior");
            AddTargetRule(byKey, "RACES/RALLY/ResultsWeekend", "Data", "State 8", 2, "Amateur");
            AddTargetRule(byKey, "RACES/RALLY/ResultsWeekend", "Data", "State 9", 2, "Junior");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "State 2", 5, "Junior");

            // Sheets: Traffic Ticket (DUI / speeding fine descriptions)
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "Rattijuopumus. Puhelluskokeessa");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "promillea.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "DUI. Alc. breath test");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "per mille.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "Ylinopeus. 100km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "km/h.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "km/h at 100km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "km/h 80km/h rajoituksella.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 5, "km/h at 80km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 4", 8, "litraa lietettä kaadettu maastoon.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 4", 9, "Illegal dumping of waste,");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 4", 9, "litres.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 4, "km/h 45km/h rajoituksella.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 5, "km/h at 45km/h vehicle limit.");

            // Sheets: Enviro Crime ticket
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 8, "litraa lietett\u00e4 kaadettu maastoon.");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 9, "Illegal dumping of waste,");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 9, "litres.");

            // COMPUTER: POS boot / shell command output
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 1", 0, "Starting RS-POS...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 3", 0, "HIMEM is testing extended memory...done.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 4", 0, "Copyright (C) Royalsoft Corp 1982-1991. All Rights Reserved.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 5", 0, "Megamedia Pro Family, v.2.45 Copyright (C) 1992, All Rights Reserved.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Error", 0, "The system cannot find the path specified.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Error 2", 0, "Incorrect command.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Format disk", 0, "Formatting... 0%");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Format drive", 0, "Formatting... 0%");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Copy disk", 1, "Copying...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Data error", 1, "Data error reading drive A");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Write new line 2", 0, "NOT CONNECTED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Reset POS 2", 0, "Quit (exit) Call (atdt #) Baud (mode baud=*)");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Error 2", 0, "Not enough memory");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Calling...", 0, "ESTABLISHING CONNECTION...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Waiting...", 0, "CONNECTION ESTABLISHED: WAITING");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Calling....", 0, "ESTABLISHING CONNECTION...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Wrong number", 0, "COULD NOT CONNECT");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Incorrect", 0, "INCORRECT BAUD SETTING");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "New baud", 0, "BAUD SETTING CHANGED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Mem error", 1, "Not enough memory");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Copyying", 4, "Copying...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Remove mem", 3, "Formatting...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Remove mem 2", 3, "Formatting...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Dir list A", 3, "Volume in drive A is A");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Dir list A", 2, "Volume in drive A is A");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Dir list C", 3, "Volume in drive C is C");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "Spezzer", 1, "EN JOY! ING UR 'PUTER? DIS IS SPE77ER SPOOKING!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "State 3", 1, ":::::FUCK UR P0RN MAKE YA MOMMA BUY U NEW 'PUTER:::: HA HA:::: SPE77ER DA NAME!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "Use", "State 1", 0, "Insert boot disk and press RETURN...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "Use", "State 3", 0, "Error reading disk.");

            // COMPUTER: TELEBBS chat/status
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", "", "State 1", 0, "NOT CONNECTED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", "", "", -1, "CONNECTION CLOSED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "Typer", "", -1, "CONNECTION CLOSED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Wait", 0, "Press RETURN to set your handle");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Too short", 0, "Needs to be at least one characters long!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Download", 1, "Sending...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Fail", 0, "Sending Failed!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Upload", 1, "Sending...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "", -1, "CONNECTION CLOSED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "", -1, "Press RETURN to set your handle");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "", -1, "Needs to be at least one characters long!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "", "", -1, "Insert boot disk and press RETURN...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "", "", -1, "Error reading disk.");

            // COMPUTER: Kaappis-Fishgame
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Reset", 1, "And here we go!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "k\u00e4nnikala 6", 1, "Out of beer, GAME OVER!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 1", 1, "You drink a beer.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 2", 1, "You drink another beer.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 3", 1, "Here goes another beer!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 4", 1, "Four down, two to go!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 5", 1, "You have only one beer left!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Peruskala", 0, "There's something on the hook!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Karkasi", 0, "It got away!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Ahven", 0, "That's a fine looking PERCH!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Hauki", 0, "Wow! What a PIKE!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "S\u00e4rki", 0, "A ROACH! What a waste of time!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Lahna", 0, "BREAM me up, Scotty!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Kalakukko", 0, "What's this? Flying FISHCOCK?!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Sakko", 0, "God damnit!! A FINE!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "K\u00e4nnikala", 0, "SOAK stole one of your beers!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Erikoiskala", 0, "Oh, this feels like a big one!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "UKK", 0, "It's legendary URHO KALAVA KEKKONEN!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Kultakala", 0, "Oh boy! A GOLDFISH!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Rahas\u00e4kki", 0, "Bless me bagpipes! A MONEYBAG!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Tonnikala", 0, "TON-A-FISH! Yabbadabbadoo!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Rosvo", 0, "ROBBER stole all your money! FUCK!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/MENU", "", "", -1, "Press enter");

            // COMPUTER: Kaappis-Grilli
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Grilli/Asiakkaat", "", "Game over", 0, "Game over");

            // COMPUTER: PROCYON ProPilkki
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Tuloksesi virallisessa punnituksessa:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Yhteispaino:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "My\u00f6h\u00e4styit punnituksesta! Tuloksesi mit\u00e4t\u00f6itiin.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetKisa/SuurinKala", "", "State 1", 5, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", "", "Grammat", 3, "g");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", "", "Kalan paino", 2, "g");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Alkuvalikko/Asetukset", "", "", -1, "Pelaajan Nimi");

            // COMPUTER: Kaappis-Wildvest
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "State 2", 0, "Press enter");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "State 2", 0, "Win! Press enter");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "Lose", 0, "YOU LOSE");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "Lose", 0, "Traumatized! Game over!");

            // COMPUTER: RAMI Simppa&Jokke adventure text
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Antenna", "", "", -1, "Antenna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle", "", "", -1, "Wine bottle");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle", "", "", -1, "Sacramental wine");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Oven", "", "", -1, "Oven");
        }
    }
}
