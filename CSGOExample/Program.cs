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

namespace CSGOExample
{
    class Program
    {

        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw
        public static IntPtr client_panorama = IntPtr.Zero;
        public static IntPtr client_panorama_size = IntPtr.Zero;
        public static IntPtr dwViewMatrix_Offs = IntPtr.Zero;
        public static IntPtr dwEntityList_Offs = IntPtr.Zero;
        public static IntPtr dwLocalPlayer_Offs = IntPtr.Zero;

        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }

        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuColor CTColor = new MenuColor("ctcolor", "CT ESP Color", new SharpDX.Color(0, 0, 255));
                public static readonly MenuBool DrawAlliesEsp = new MenuBool("drawalbox", "Draw Allies ESP", true);
                public static readonly MenuColor TRColor = new MenuColor("tercolor", "Terrorist ESP Color", new SharpDX.Color(255, 0, 0));
                public static readonly MenuBool DrawBox = new MenuBool("drawbox", "Draw Box ESP", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);
                public static readonly MenuBool DrawBoxHP = new MenuBool("drawboxhp", "Draw Health", true);
                public static readonly MenuSliderBool DrawTextSize = new MenuSliderBool("drawtextsize", "Text Size", false, 14, 4, 72);
                public static readonly MenuBool DrawTextDist = new MenuBool("drawtextdist", "Draw Distance", true);
            }
        }

        public static void InitializeMenu()
        {
            VisualsMenu = new Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.CTColor,
                Components.VisualsComponent.DrawAlliesEsp.SetToolTip("Really great feature to increase performance by the way!"),
                Components.VisualsComponent.TRColor,
                Components.VisualsComponent.DrawBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
                Components.VisualsComponent.DrawBoxHP,
                Components.VisualsComponent.DrawTextSize,
                Components.VisualsComponent.DrawTextDist,
            };

            RootMenu = new Menu("csgoexample", "WeScript.app CSGO Example Assembly", true)
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
            Console.WriteLine("WeScript.app CSGO Example Assembly Loaded!");

            InitializeMenu();
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }


        private static void OnTick(int counter, EventArgs args)
        {
            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = Memory.FindWindowName("Counter-Strike: Global Offensive"); //try finding the window of the process (check if it's spawned and loaded)
                if (wndHnd != IntPtr.Zero) //if it exists
                {
                    var calcPid = Memory.GetPIDFromHWND(wndHnd); //get the PID of that same process
                    if (calcPid > 0) //if we got the PID
                    {
                        processHandle = Memory.OpenProcess(PROCESS_ALL_ACCESS, calcPid); //get full access to the process so we can use it later
                        if (processHandle != IntPtr.Zero)
                        {
                            //if we got access to the game, check if it's x64 bit, this is needed when reading pointers, since their size is 4 for x86 and 8 for x64
                            isWow64Process = Memory.IsProcess64Bit(processHandle);
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowName("Counter-Strike: Global Offensive");
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    isOverlayOnTop = Overlay.IsOnTop();

                    if (client_panorama == IntPtr.Zero) //if the dll is still null
                    {
                        client_panorama = Memory.GetModule(processHandle, "client_panorama.dll", isWow64Process); //attempt to find the module (if it's loaded)
                    }
                    else
                    {
                        if (client_panorama_size == IntPtr.Zero) //dll got loaded, check if size is zero
                        {
                            client_panorama_size = Memory.GetModuleSize(processHandle, "client_panorama.dll", isWow64Process); //get module size
                        }
                        else
                        {
                            if (dwViewMatrix_Offs == IntPtr.Zero) //if offset is zero... find it
                            {
                                dwViewMatrix_Offs = Memory.FindSignature(processHandle, client_panorama, client_panorama_size, "0F 10 05 ? ? ? ? 8D 85 ? ? ? ? B9", 0x3); 
                            }
                            if (dwEntityList_Offs == IntPtr.Zero) 
                            {
                                dwEntityList_Offs = Memory.FindSignature(processHandle, client_panorama, client_panorama_size, "BB ? ? ? ? 83 FF 01 0F 8C ? ? ? ? 3B F8", 0x1); 
                            }
                            if (dwLocalPlayer_Offs == IntPtr.Zero)
                            { 
                                dwLocalPlayer_Offs = Memory.FindSignature(processHandle, client_panorama, client_panorama_size, "42 56 8D 34 85 ? ? ? ? 89", 0x5);
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
                    client_panorama = IntPtr.Zero;
                    client_panorama_size = IntPtr.Zero;
                    dwViewMatrix_Offs = IntPtr.Zero;
                    dwEntityList_Offs = IntPtr.Zero;
                    dwLocalPlayer_Offs = IntPtr.Zero;
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
                var matrix = Memory.ReadMatrix(processHandle, (IntPtr)(dwViewMatrix_Offs.ToInt64() + 0xB0));
                if (dwEntityList_Offs != IntPtr.Zero)
                {
                    if (dwLocalPlayer_Offs != IntPtr.Zero)
                    {
                        var LocalPlayer = Memory.ReadPointer(processHandle, (IntPtr)(dwLocalPlayer_Offs.ToInt64() + 4), isWow64Process);
                        if (LocalPlayer != IntPtr.Zero)
                        {
                            var myPos = Memory.ReadVector3(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x138));
                            var myTeam = Memory.ReadByte(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0xF4));
                            for (uint i = 0; i <= 64; i++)
                            {
                                var entityAddr = Memory.ReadPointer(processHandle, (IntPtr)(dwEntityList_Offs.ToInt64() + i * 0x10), isWow64Process);
                                if ((entityAddr != IntPtr.Zero) && (entityAddr != LocalPlayer))
                                {
                                    //it's a bad practice to read individual offsets, instead - you should read the whole struct with 1 call
                                    var m_iHealth = Memory.ReadInt32(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x100));
                                    var bDormant = Memory.ReadBool(processHandle, (IntPtr)(entityAddr.ToInt64() + 0xED));
                                    var m_iTeamNum = Memory.ReadByte(processHandle, (IntPtr)(entityAddr.ToInt64() + 0xF4));
                                    var m_vecOrigin = Memory.ReadVector3(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x138));
                                    var f_modelHeight = Memory.ReadFloat(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x33C));

                                    if ((m_iHealth > 0) && (bDormant == false)) //entity is valid?
                                    {
                                        var headPos_fake = new Vector3(m_vecOrigin.X, m_vecOrigin.Y, m_vecOrigin.Z + f_modelHeight);
                                        Vector2 vScreen_head = new Vector2(0, 0);
                                        Vector2 vScreen_foot = new Vector2(0, 0);

                                        if (Renderer.WorldToScreen(headPos_fake, out vScreen_head, matrix, wndMargins, wndSize, W2SType.TypeD3D9)) //only draw if the head position is visible on screen
                                        {
                                            Renderer.WorldToScreen(m_vecOrigin, out vScreen_foot, matrix, wndMargins, wndSize, W2SType.TypeD3D9); //feet position does not really matter if it's visible
                                            {
                                                string dist_str = "";
                                                if (Components.VisualsComponent.DrawTextDist.Enabled)
                                                {
                                                    double playerDist = GetDistance3D(myPos, m_vecOrigin) / 22.0f;
                                                    dist_str = $"[{playerDist.ToString("0.0")}]"; //only 1 demical number after the dot
                                                }
                                                if (Components.VisualsComponent.DrawTheVisuals.Enabled)
                                                {
                                                    if ((!Components.VisualsComponent.DrawAlliesEsp.Enabled) && (myTeam != m_iTeamNum)) continue; //skil allies
                                                    Renderer.DrawFPSBox(vScreen_head, vScreen_foot, (m_iTeamNum == 3) ? Components.VisualsComponent.CTColor.Color : Components.VisualsComponent.TRColor.Color, (f_modelHeight == 54.0f) ? BoxStance.crouching : BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled, Components.VisualsComponent.DrawBox.Enabled, m_iHealth, Components.VisualsComponent.DrawBoxHP.Enabled ? 100 : 0, 0, 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, string.Empty, string.Empty, string.Empty, string.Empty);
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
