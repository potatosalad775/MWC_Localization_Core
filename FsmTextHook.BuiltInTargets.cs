using System.Collections.Generic;

namespace MWC_Localization_Core
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
            AddTargetRule(byKey, "Radio/Folk", "", "", -1, "NOT IMPORTED");
            AddTargetRule(byKey, "Radio/Folk", "", "", -1, "RADIO NOT IMPORTED");
            AddTargetRule(byKey, "Radio/Folk", "", "Off", 1, "RADIO IMPORTED");
            AddTargetRule(byKey, "Radio/Folk", "", "Off", 1, "RADIO NOT IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "", -1, "NOT IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "", -1, "CD NOT IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "State 1", 0, "CD'S IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "State 1", 0, "CD NOT IMPORTED");

            // TV Teletext pages (240/241/302 results, Teletext command bar)
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1", "Data", "State 1", 2, "P\u00e4\u00e4sarjan kierroksen");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1", "Data", "State 1", 2, "tulokset.");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1", "Data", "State 1", 2, "Sarjatilanne kun pelattu");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1", "Data", "State 1", 2, "ottelua.");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1", "Data", "State 1", 2, "Kierros");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1", "Data", "State 1", 2, "tulokset");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1", "Data", "State 1", 3, "Kierros");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1", "Data", "State 1", 3, "pelikohteet");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1", "Data", "", -1, "P\u00e4\u00e4sarjan kierroksen");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1", "Data", "", -1, "tulokset.");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1", "Data", "", -1, "Sarjatilanne kun pelattu");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1", "Data", "", -1, "ottelua.");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1", "Data", "", -1, "Kierros");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1", "Data", "", -1, "tulokset");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1", "Data", "", -1, "Kierros");
            AddTargetRule(byKey, "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1", "Data", "", -1, "pelikohteet");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "", -1, "stop");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "", -1, "haku");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "Load", 0, "stop");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "Load", 0, "haku");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "Open page", 0, "stop");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "Open page", 0, "haku");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "State 1", 4, "stop");
            AddTargetRule(byKey, "Systems/TV/Teletext", "", "State 1", 4, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "", "", -1, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "", -1, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "", "Load", 0, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "Load", 0, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "", "Open page", 0, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "Open page", 0, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "", "State 1", 4, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "State 1", 4, "haku");

            // TV Graphics: CHAT day/time, GFXTanaan schedule overlays (runtime-dynamic)
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day/Time", "Clock", "State 3", 2, "KLO");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day", "Text", "State 11", 0, "maanantai");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day", "Text", "State 11", 0, "tiistai");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day", "Text", "State 11", 0, "keskiviikko");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day", "Text", "State 11", 0, "torstai");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day", "Text", "State 11", 0, "perjantai");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day", "Text", "State 11", 0, "lauantai");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Day", "Text", "State 11", 0, "sunnuntai");
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanWeek/Text", "Text", "", -1, "ohjelmat", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanWeek/Text", "Text", "", -1, "maanantai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanWeek/Text", "Text", "", -1, "tiistai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanWeek/Text", "Text", "", -1, "keskiviikko", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanWeek/Text", "Text", "", -1, "torstai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanWeek/Text", "Text", "", -1, "perjantai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSat1/Text", "Text", "", -1, "ohjelmat", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSun1/Text", "Text", "", -1, "ohjelmat", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSat1/Text", "Text", "", -1, "lauantai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSun1/Text", "Text", "", -1, "sunnuntai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSat2/Text", "Text", "", -1, "ohjelmat", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSun2/Text", "Text", "", -1, "ohjelmat", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSat2/Text", "Text", "", -1, "lauantai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/GFXTanaanSun2/Text", "Text", "", -1, "sunnuntai", runtimeDynamic: true);
            AddTargetRule(byKey, "Systems/TV/TVGraphics/CHAT/Moderator", "Text", "State 11", 1, "Valvojana:");

            // Sheets: Rally results / registration / penalties (runtime-dynamic where game rebuilds rows)
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "", -1, "Junior", runtimeDynamic: true);
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "", -1, "Amateur", runtimeDynamic: true);
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "", -1, "- Class", runtimeDynamic: true);
            AddTargetRule(byKey, "Sheets/RallyRegistration/Functions/Class", "Data", "", -1, "Junior", runtimeDynamic: true);
            AddTargetRule(byKey, "Sheets/RallyRegistration/Functions/Class", "Data", "", -1, "Amateur", runtimeDynamic: true);
            AddTargetRule(byKey, "Sheets/RallyRegistration/Functions/Class", "Data", "", -1, "- Class", runtimeDynamic: true);
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "", -1, "Time penalty:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "", -1, "sec.");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "", -1, "Parc Ferme violation:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "", -1, "Jump start violation:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 6, "Time penalty:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 6, "sec.");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 7, "Parc Ferme violation:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 8, "Jump start violation:");

            // Sheets: Traffic Ticket (DUI / speeding fine descriptions)
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "DUI. Alc. breath test");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "Rattijuopumus. Puhelluskokeessa");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "per mille.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "promillea.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "Rattijuopumus. Puhelluskokeessa");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "DUI. Alc. breath test");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "promillea.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "per mille.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 6, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "km/h 80km/h rajoitetulla");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "km/h at 80km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "km/h at 80km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 6, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 5, "km/h at 80km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 6, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 4, "km/h 80km/h rajoitetulla");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "km/h 80km/h rajoituksella.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 5, "km/h at 80km/h limit zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 6, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 4", 10, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "DUI. Alc. breath test");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Rattijuopumus. Puhelluskokeessa");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "per mille.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "promillea.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 80km/h limit zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 80km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 80km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h 80km/h rajoitetulla");

            // Sheets: Enviro Crime ticket
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 8, "litraa lietett\u00e4 kaadettu maastoon.");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 9, "Illegal dumping of waste,");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 9, "litres.");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Fetch data", 11, "litraa lietett\u00e4 kaadettu maastoon.");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Fetch data", 11, "Illegal dumping of waste,");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Fetch data", 11, "litres.");

            // ATM transaction description labels
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Vuokra");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Jakopalkkio");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Nosto");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Asumistuki");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "PSKPerajarviAutom");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "PSK Pera Autom.");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Ty\u00f6tt\u00f6myystuki");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "TaksiPalkka");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Palkka");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Talletuskorko");
            AddTargetRule(byKey, "PERAPORTTI/ActiveFunctions/ATMs/MoneyATM/Screen/Tapahtumat/Tapahtumat/Selite", "GetData", "", -1, "Talletus");

            // COMPUTER: POS boot / shell command output
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 1", 0, "Starting RS-POS...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 3", 0, "HIMEM is testing extended memory...done.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 4", 0, "Copyright (C) Royalsoft Corp 1982-1991. All Rights Reserved.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/BootSequence", "Use", "State 5", 0, "Megamedia Pro Family, v.2.45 Copyright (C) 1992, All Rights Reserved.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Error", 0, "Incorrect command.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Error", 0, "The system cannot find the path specified.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Error 2", 0, "Incorrect command.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Format disk", 0, "Formatting... 0%");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Format drive", 0, "Formatting... 0%");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Copy disk", 1, "Copying...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Data error", 1, "Data error reading drive A");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Write new line 2", 0, "NOT CONNECTED");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Reset POS 2", 0, "Quit (exit) Call (atdt #) Baud (mode baud=*)");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Error 2", 0, "Not enough memory");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Calling...", 0, "ESTABLISHING CONNECTION...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Waiting...", 0, "CONNECTION ESTABLISHED: WAITING");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Calling....", 0, "ESTABLISHING CONNECTION...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Wrong number", 0, "COULD NOT CONNECT");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Incorrect", 0, "INCORRECT BAUD SETTING");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "New baud", 0, "BAUD SETTING CHANGED");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Mem error", 1, "Not enough memory");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Copyying", 4, "Copying...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Remove mem", 3, "Formatting...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Remove mem 2", 3, "Formatting...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Dir list A", 3, "Volume in drive A is A");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Dir list A", 2, "Volume in drive A is A");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Dir list C", 3, "Volume in drive C is C");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "Spezzer", 1, "EN JOY! ING UR 'PUTER? DIS IS SPE77ER SPOOKING!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "State 3", 1, ":::::FUCK UR P0RN MAKE YA MOMMA BUY U NEW 'PUTER:::: HA HA:::: SPE77ER DA NAME!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/NoOS", "", "State 1", 0, "Insert boot disk and press RETURN...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/NoOS", "", "State 3", 0, "Error reading disk.");

            // COMPUTER: TELEBBS chat/status
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", "", "State 1", 0, "NOT CONNECTED");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", "", "", -1, "CONNECTION CLOSED");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/Command", "Typer", "", -1, "CONNECTION CLOSED");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Wait", 0, "Press RETURN to set your handle");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Too short", 0, "Needs to be at least one characters long!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Download", 1, "Sending...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Fail", 0, "Sending Failed!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "Upload", 1, "Sending...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "Type", "", -1, "CONNECTION CLOSED");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "", -1, "Press RETURN to set your handle");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "", -1, "Needs to be at least one characters long!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/NoOS", "", "", -1, "Insert boot disk and press RETURN...");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/POS/NoOS", "", "", -1, "Error reading disk.");

            // COMPUTER: Kaappis-Fishgame
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Reset", 1, "And here we go!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "k\u00e4nnikala 6", 1, "Out of beer, GAME OVER!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 1", 1, "You drink a beer.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 2", 1, "You drink another beer.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 3", 1, "Here goes another beer!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 4", 1, "Four down, two to go!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 5", 1, "You have only one beer left!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Peruskala", 0, "There's something on the hook!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Karkasi", 0, "It got away!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Ahven", 0, "That's a fine looking PERCH!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Hauki", 0, "Wow! What a PIKE!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "S\u00e4rki", 0, "A ROACH! What a waste of time!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Lahna", 0, "BREAM me up, Scotty!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Kalakukko", 0, "What's this? Flying FISHCOCK?!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Sakko", 0, "God damnit!! A FINE!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "K\u00e4nnikala", 0, "SOAK stole one of your beers!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Erikoiskala", 0, "Oh, this feels like a big one!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "UKK", 0, "It's legendary URHO KALAVA KEKKONEN!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Kultakala", 0, "Oh boy! A GOLDFISH!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Rahas\u00e4kki", 0, "Bless me bagpipes! A MONEYBAG!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Tonnikala", 0, "TON-A-FISH! Yabbadabbadoo!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Rosvo", 0, "ROBBER stole all your money! FUCK!");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Fishgame/MENU", "", "", -1, "Press enter");

            // COMPUTER: Kaappis-Grilli
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Grilli/Asiakkaat", "", "Game over", 0, "Game over");

            // COMPUTER: PROCYON ProPilkki
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Pelaajan Nimi");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Ahven");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Kiiski");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Lahna");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Siika");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "S\u00e4rki");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Hauki");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Tuloksesi virallisessa punnituksessa:");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Yhteispaino:");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "Suurin kala:");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "", -1, "My\u00f6h\u00e4styit punnituksesta! Tuloksesi mit\u00e4t\u00f6itiin.");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Pelaajan Nimi");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Ahven");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Kiiski");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Lahna");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Siika");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "S\u00e4rki");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "", -1, "Hauki");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetKisa/SuurinKala", "", "State 1", 5, "Suurin kala:");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", "", "Grammat", 3, "g");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", "", "Kalan paino", 2, "g");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/PROCYON-ProPilkki/Alkuvalikko/Asetukset", "", "", -1, "Pelaajan Nimi");

            // COMPUTER: Kaappis-Wildvest
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "State 2", 0, "Press enter");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "State 2", 0, "Win! Press enter");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "Lose", 0, "YOU LOSE");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "Lose", 0, "Traumatized! Game over!");

            // COMPUTER: RAMI Simppa&Jokke adventure text
            AddTargetRule(byKey, "COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Antenna", "", "", -1, "Antenna");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle", "", "", -1, "Wine bottle");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle", "", "", -1, "Sacramental wine");
            AddTargetRule(byKey, "COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Oven", "", "", -1, "Oven");

            // Sheets: Service Payment line items (Fleetari breakdown source)
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Vanteiden kiilotus / Rim polish");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Rengasty\u00f6t / tire job");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Custom automaalaus / Custom paint");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Metalliv\u00e4ri / Metallic color");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Alkuper\u00e4isv\u00e4ri / original color");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Tehtaan erikoismaalaus / factory special paint");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Vanteet metalliv\u00e4ri / Rim metallic");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Vanteet maalattuna / Rim paint");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Moottorin s\u00e4\u00e4t\u00f6 / Engine adjust");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Aurauskulmien s\u00e4\u00e4t\u00f6 / Toe adjust");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Jarruhuolto / brake service");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Moottorin viritys / engine tune up");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Ripustusten suoristus / susp. repair");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Ovien turvaverkot / door safety nets");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Turvakehikon asennus / rollcage install");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Tuulilasin vaihto / windshield replacement");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Per\u00e4v\u00e4lityksen vaihto / ratio change");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Turvakehikon poisto / rollcage removal");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Peltity\u00f6t / sheet metal work");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Vinyylikaton poisto / vinyl removal");
            AddTargetRule(byKey, "Sheets/ServicePayment/Line", "GetLine", "", -1, "Mittatilausjouset / Coil spring order");
        }
    }
}
