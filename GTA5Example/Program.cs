using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics;
using SharpDX.XInput;
using WeScriptWrapper;
using WeScript.SDK.UI;
using WeScript.SDK.UI.Components;

namespace GTA5Example
{
    class Program
    {

        enum ped_types
        {
            PLAYER_0, // michael
            PLAYER_1, // franklin
            NETWORK_PLAYER, // mp character
            PLAYER_2, // trevor
            CIVMALE,
            CIVFEMALE,
            COP,
            GANG_ALBANIAN,
            GANG_BIKER_1,
            GANG_BIKER_2,
            GANG_ITALIAN,
            GANG_RUSSIAN,
            GANG_RUSSIAN_2,
            GANG_IRISH,
            GANG_JAMAICAN,
            GANG_AFRICAN_AMERICAN,
            GANG_KOREAN,
            GANG_CHINESE_JAPANESE,
            GANG_PUERTO_RICAN,
            DEALER,
            MEDIC,
            FIREMAN,
            CRIMINAL,
            BUM,
            PROSTITUTE,
            SPECIAL,
            MISSION,
            SWAT,
            ANIMAL,
            ARMY
        };

        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw
        public static IntPtr GameBase = IntPtr.Zero;
        public static IntPtr GameSize = IntPtr.Zero;
        public static IntPtr dwViewMatrix_Offs = IntPtr.Zero;
        public static IntPtr dwReplayInterface_Offs = IntPtr.Zero;
        public static IntPtr dwWorld_Offs = IntPtr.Zero;
        public static IntPtr localPlayer = IntPtr.Zero;


        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }

        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuColor PLAYERSColor = new MenuColor("playerscolor", "PLAYERS ESP Color", new SharpDX.Color(255, 0, 0));
                public static readonly MenuBool DrawPlayersESP = new MenuBool("drawplayersesp", "Draw PLAYERS ESP", true);
                public static readonly MenuSlider PlayersDistanceRend = new MenuSlider("distslider", "Maximum Players Distance Render", 500, 10, 5000);
                public static readonly MenuColor NPCColor = new MenuColor("npccolor", "NPC ESP Color", new SharpDX.Color(255, 255, 255, 100));
                public static readonly MenuBool DrawNPCESP = new MenuBool("drawnpcesp", "Draw NPC ESP", true);
                public static readonly MenuBool DrawBox = new MenuBool("drawbox", "Draw Box ESP", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);
                public static readonly MenuBool DrawBoxHP = new MenuBool("drawboxhp", "Draw HealthBar", true);
                public static readonly MenuSliderBool DrawTextSize = new MenuSliderBool("drawtextsize", "Text Size", false, 14, 4, 72);
                public static readonly MenuBool DrawTextDist = new MenuBool("drawtextdist", "Draw Distance Text", true);
                public static readonly MenuBool DrawTextHealth = new MenuBool("drawtexthp", "Draw Distance Health", true);
            }
        }

        public static void InitializeMenu()
        {
            VisualsMenu = new Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.PLAYERSColor,
                Components.VisualsComponent.DrawPlayersESP,
                Components.VisualsComponent.PlayersDistanceRend,
                Components.VisualsComponent.NPCColor,
                Components.VisualsComponent.DrawNPCESP,
                Components.VisualsComponent.DrawBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
                Components.VisualsComponent.DrawBoxHP,
                Components.VisualsComponent.DrawTextSize,
                Components.VisualsComponent.DrawTextDist,
                Components.VisualsComponent.DrawTextHealth,
            };

            RootMenu = new Menu("gtavexample", "WeScript.app GTA5 Example Assembly", true)
            {
                Components.MainAssemblyToggle.SetToolTip("The magical boolean which completely disables/enables the assembly!"),
                VisualsMenu,
            };
            RootMenu.Attach();
        }
        private static double GetDistance3D(Vector3 myPos, Vector3 enemyPos)
        {
            Vector3 vector = new Vector3(myPos.X - enemyPos.X, myPos.Y - enemyPos.Y, myPos.Z - enemyPos.Z);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("WeScript.app GTA5 Example Assembly Loaded! [Last update 15.01.2022]");
            InitializeMenu();
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }


        private static void OnTick(int counter, EventArgs args)
        {
            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = Memory.FindWindowClassName("grcWindow"); //using classname in hope it's the same for FiveM or other mods
                if (wndHnd != IntPtr.Zero) //if it exists
                {
                    var calcPid = Memory.GetPIDFromHWND(wndHnd); //get the PID of that same process
                    if (calcPid > 0) //if we got the PID
                    {
                        processHandle = Memory.OpenProcess(PROCESS_ALL_ACCESS, calcPid); //get full access to the process so we can use it later
                        if (processHandle != IntPtr.Zero)
                        {
                            //if we got access to the game, check if it's x64 bit, this is needed when reading pointers, since their size is 4 for x86 and 8 for x64
                            isWow64Process = Memory.IsProcess64Bit(processHandle); //we know GTA5 is 64 bit but anyway...
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowClassName("grcWindow");
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    isOverlayOnTop = Overlay.IsOnTop();

                    if (GameBase == IntPtr.Zero) //do we have access to Gamebase address?
                    {
                        GameBase = Memory.GetModule(processHandle, null, isWow64Process); //if not, find it
                    }
                    else
                    {
                        if (GameSize == IntPtr.Zero)
                        {
                            GameSize = Memory.GetModuleSize(processHandle, null, isWow64Process);
                        }
                        else
                        {
                            if (dwViewMatrix_Offs == IntPtr.Zero)
                            {
                                dwViewMatrix_Offs = Memory.FindSignature(processHandle, GameBase, GameSize, "48 8B 15 ? ? ? ? 48 8D 2D ? ? ? ? 48 8B CD", 0x3);
                                Console.WriteLine($"dwViewMatrix_Offs: {dwViewMatrix_Offs.ToString("X")}");
                            }
                            if (dwReplayInterface_Offs == IntPtr.Zero)
                            {
                                dwReplayInterface_Offs = Memory.FindSignature(processHandle, GameBase, GameSize, "48 8B 05 ? ? ? ? 41 8B 1E", 0xF0);
                                Console.WriteLine($"dwReplayInterface_Offs: {dwReplayInterface_Offs.ToString("X")}");
                            }
                            if (dwWorld_Offs == IntPtr.Zero)
                            {
                                dwWorld_Offs = Memory.FindSignature(processHandle, GameBase, GameSize, "48 8B 05 ? ? ? ? 45 ? ? ? ? 48 8B 48 08 48 85 C9 74 07", 0x3);
                                Console.WriteLine($"dwWorld_Offs: {dwWorld_Offs.ToString("X")}");
                            }
                        }
                    }

                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(processHandle); //close the handle to avoid leaks
                    processHandle = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;

                    //clear your offsets, modules
                    GameBase = IntPtr.Zero;
                    GameSize = IntPtr.Zero;
                    dwViewMatrix_Offs = IntPtr.Zero;
                    dwReplayInterface_Offs = IntPtr.Zero;
                    dwWorld_Offs = IntPtr.Zero;
                    localPlayer = IntPtr.Zero;
                }
            }
        }

        private static void OnRenderer(int fps, EventArgs args)
        {
            if (!gameProcessExists) return; //process is dead, don't bother drawing
            if ((!isGameOnTop) && (!isOverlayOnTop)) return; //if game and overlay are not on top, don't draw
            if (!Components.MainAssemblyToggle.Enabled) return; //main menu boolean to toggle the cheat on or off

            if (dwViewMatrix_Offs != IntPtr.Zero)
            {
                var matrix = Memory.ReadMatrix(processHandle, (IntPtr)(dwViewMatrix_Offs.ToInt64() + 0x24C + 0x30));
                if (dwWorld_Offs != IntPtr.Zero)
                {
                    var worldPtr = Memory.ReadPointer(processHandle, dwWorld_Offs, isWow64Process);
                    if (worldPtr != IntPtr.Zero)
                    {
                        localPlayer = Memory.ReadPointer(processHandle, (IntPtr)(worldPtr.ToInt64() + 0x8), isWow64Process);
                    }
                }
                if ((dwReplayInterface_Offs != IntPtr.Zero) && (localPlayer != IntPtr.Zero))
                {
                    var localPos = Memory.ReadVector3(processHandle, (IntPtr)localPlayer.ToInt64() + 0x90);
                    var replay_interface_t = Memory.ReadPointer(processHandle, dwReplayInterface_Offs, isWow64Process);
                    if (replay_interface_t != IntPtr.Zero)
                    {
                        var PedList = Memory.ReadPointer(processHandle, (IntPtr)(replay_interface_t.ToInt64() + 0x18), isWow64Process);
                        if (PedList != IntPtr.Zero)
                        {
                            var list_ptr = Memory.ReadPointer(processHandle, (IntPtr)(PedList.ToInt64() + 0x100), isWow64Process);
                            var max_ptrs = Memory.ReadInt32(processHandle, (IntPtr)(PedList.ToInt64() + 0x108));
                            if (list_ptr != IntPtr.Zero)
                            {
                                if (max_ptrs > 0)
                                {
                                    for (uint i = 0; i <= max_ptrs; i++)
                                    {
                                        var ped = Memory.ReadPointer(processHandle, (IntPtr)(list_ptr.ToInt64() + i * 0x10), isWow64Process);
                                        if ((ped != IntPtr.Zero) && (ped != localPlayer))
                                        {
                                            var entityPos = Memory.ReadVector3(processHandle, (IntPtr)ped.ToInt64() + 0x90);
                                            var entity_feet = new Vector3(entityPos.X, entityPos.Y, entityPos.Z - 1.0f);
                                            var entity_head = new Vector3(entityPos.X, entityPos.Y, entityPos.Z + 0.8f);
                                            var entityHP = Memory.ReadFloat(processHandle, (IntPtr)ped.ToInt64() + 0x280);
                                            var entityMaxHP = Memory.ReadFloat(processHandle, (IntPtr)ped.ToInt64() + 0x2A0);
                                            var ped_type = Memory.ReadUInt32(processHandle, (IntPtr)ped.ToInt64() + 0x10B8); //c1 e2 0b c1 fa 19 e9 //sig to find it manually
                                            if (ped_type > 0)
                                            {
                                                ped_type = (ped_type << 11 >> 25);
                                            }
                                            if (Components.VisualsComponent.DrawTheVisuals.Enabled)
                                            {
                                                if (entityHP > 0.0f)
                                                {
                                                    string dist_str = "";
                                                    string health_str = "";
                                                    double playerDist = GetDistance3D(localPos, entityPos);
                                                    if (Components.VisualsComponent.DrawTextDist.Enabled)
                                                    {
                                                        dist_str = $"[{playerDist.ToString("0.0")}]"; //only 1 demical number after the dot
                                                    }
                                                    if (Components.VisualsComponent.DrawTextHealth.Enabled)
                                                    {
                                                        health_str = $"[{entityHP.ToString("0")}/{entityMaxHP.ToString("0")}]";
                                                    }
                                                    Vector2 vScreen_head = new Vector2(0, 0);
                                                    Vector2 vScreen_foot = new Vector2(0, 0);
                                                    if (Renderer.WorldToScreen(entity_head, out vScreen_head, matrix, wndMargins, wndSize, W2SType.TypeD3D11)) //only draw if the head position is visible on screen
                                                    {
                                                        Renderer.WorldToScreen(entity_feet, out vScreen_foot, matrix, wndMargins, wndSize, W2SType.TypeD3D11); //feet position does not really matter if it's visible
                                                        {
                                                            if (Components.VisualsComponent.DrawPlayersESP.Enabled)
                                                            {
                                                                if (ped_type == (uint)ped_types.NETWORK_PLAYER)
                                                                {
                                                                    if (playerDist < Components.VisualsComponent.PlayersDistanceRend.Value)
                                                                    {
                                                                        Renderer.DrawFPSBox(vScreen_head, vScreen_foot, Components.VisualsComponent.PLAYERSColor.Color, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled, Components.VisualsComponent.DrawBox.Enabled, entityHP, Components.VisualsComponent.DrawBoxHP.Enabled ? entityMaxHP : 0, 0, 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, health_str, string.Empty, string.Empty, string.Empty);
                                                                    }
                                                                }
                                                            }
                                                            if (Components.VisualsComponent.DrawNPCESP.Enabled)
                                                            {
                                                                if ((ped_type != (uint)ped_types.NETWORK_PLAYER) && (ped_type != (uint)ped_types.ANIMAL)) //draw NPC and ignore animals
                                                                {
                                                                    Renderer.DrawFPSBox(vScreen_head, vScreen_foot, Components.VisualsComponent.NPCColor.Color, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled, Components.VisualsComponent.DrawBox.Enabled, entityHP, Components.VisualsComponent.DrawBoxHP.Enabled ? entityMaxHP : 0, 0, 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, health_str, string.Empty, string.Empty, string.Empty);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
