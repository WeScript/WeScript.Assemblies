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
using System.Runtime.InteropServices; //for StructLayout
using System.Security.Permissions;

namespace AssaultCubeExample
{
    class Program
    {

        //[StructLayout(LayoutKind.Explicit)]
        //public struct GameEntityStruct
        //{
        //    [FieldOffset(0x04)]
        //    public Vector3 headPos;
        //    [FieldOffset(0x34)]
        //    public Vector3 feetPos;
        //    [FieldOffset(0xF8)]
        //    public Int32 health;
        //    [FieldOffset(0xFC)]
        //    public Int32 armor;
        //    [FieldOffset(0x32C)]
        //    public Int32 team;
        //}

        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw 

        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }

        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuColor AlliesColor = new MenuColor("alliescolor", "Allies ESP Color", new SharpDX.Color(0, 0, 255));
                public static readonly MenuBool DrawAlliesEsp = new MenuBool("drawbox", "Draw Allies ESP", true);
                public static readonly MenuColor EnemiesColor = new MenuColor("enemiescolor", "Enemies ESP Color", new SharpDX.Color(255, 0, 0));
                public static readonly MenuBool DrawBox = new MenuBool("drawbox", "Draw Box ESP", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);
                public static readonly MenuBool DrawBoxHP = new MenuBool("drawboxhp", "Draw Health", true);
                public static readonly MenuBool DrawBoxAR = new MenuBool("drawboxar", "Draw Armor", true);
                public static readonly MenuSliderBool DrawTextSize = new MenuSliderBool("drawtextsize", "Text Size", false, 14, 4, 72);
                public static readonly MenuBool DrawTextDist = new MenuBool("drawtextdist", "Draw Distance", true);
                public static readonly MenuBool DrawTextName = new MenuBool("drawtextname", "Draw Player Name", true);
            }
        }

        public static void InitializeMenu()
        {
            VisualsMenu = new Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.AlliesColor,
                Components.VisualsComponent.DrawAlliesEsp.SetToolTip("Don't forget that disabling this feature in a regular dm server will still erase half of the players!"),
                Components.VisualsComponent.EnemiesColor,
                Components.VisualsComponent.DrawBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
                Components.VisualsComponent.DrawBoxHP,
                Components.VisualsComponent.DrawBoxAR,
                Components.VisualsComponent.DrawTextSize,
                Components.VisualsComponent.DrawTextDist,
                Components.VisualsComponent.DrawTextName,
            };

            RootMenu = new Menu("assaultcubeexample", "WeScript.app AssaultCubeExample Assembly", true)
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
            Console.WriteLine("WeScript.app AssaultCubeExample Assembly Loaded!");
            
            InitializeMenu();
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }


        private static void OnTick(int counter, EventArgs args)
        {
            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = Memory.FindWindowName("AssaultCube"); //try finding the window of the process (check if it's spawned and loaded)
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
                            //here you can scan for signatures and stuff, it happens only once on "attach"
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowName("AssaultCube");
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    isOverlayOnTop = Overlay.IsOnTop();
                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(processHandle); //close the handle to avoid leaks
                    processHandle = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;
                }
            }
        }

        private static void OnRenderer(int fps, EventArgs args)
        {
            if (!gameProcessExists) return; //process is dead, don't bother drawing
            if ((!isGameOnTop) && (!isOverlayOnTop)) return; //if game and overlay are not on top, don't draw
            if (!Components.MainAssemblyToggle.Enabled) return; //main menu boolean to toggle the cheat on or off


            var matrix = Memory.ReadMatrix(processHandle, (IntPtr)0x501AE8);
            var EntityListPtr = Memory.ReadPointer(processHandle, (IntPtr)0x50F4F8, isWow64Process);
            var LocalPlayer = Memory.ReadPointer(processHandle, (IntPtr)0x509B74, isWow64Process);
            int myTeam = 0;
            Vector3 myPos = new Vector3(0, 0, 0);
            if (LocalPlayer != IntPtr.Zero)
            {
                myTeam = Memory.ReadInt32(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x32C));
                myPos = Memory.ReadVector3(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x04));
            }

            if (EntityListPtr != IntPtr.Zero)
            {
                var entityCount = Memory.ReadUInt32(processHandle, (IntPtr)0x50F500);
                if (entityCount > 0)
                {
                    for (uint i = 0; i <= entityCount; i++)
                    {
                        var entityAddr = Memory.ReadPointer(processHandle, (IntPtr)(EntityListPtr.ToInt64() + i * 4), isWow64Process);
                        if (entityAddr != IntPtr.Zero)
                        {
                            //it's a bad practice to read individual offsets, instead - you should read the whole struct with 1 call
                            var headpos = Memory.ReadVector3(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x04));
                            var feetpos = Memory.ReadVector3(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x034));
                            var stanceFlt = Memory.ReadFloat(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x05C));
                            var health = Memory.ReadInt32(processHandle, (IntPtr)(entityAddr.ToInt64() + 0xF8));
                            var armor = Memory.ReadInt32(processHandle, (IntPtr)(entityAddr.ToInt64() + 0xFC));
                            var playerName = Memory.ReadString(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x225), false);
                            var playerTeam = Memory.ReadInt32(processHandle, (IntPtr)(entityAddr.ToInt64() + 0x32C));
                            headpos.Z += 1.0f; //hackish method to "lift up" the head position so boxes look cleaner
                            if ((health >= 1) && (health <= 100)) //dummy isAlive check for entities
                            {
                                Vector2 vScreen_head = new Vector2(0, 0); //placeholder for screen coords of player head
                                Vector2 vScreen_foot = new Vector2(0, 0);
                                if (Renderer.WorldToScreen(headpos, out vScreen_head, matrix, wndMargins, wndSize, W2SType.TypeOGL)) //only draw if the head position is visible on screen
                                {
                                    Renderer.WorldToScreen(feetpos, out vScreen_foot, matrix, wndMargins, wndSize, W2SType.TypeOGL); //feet position does not really matter if it's visible
                                    {
                                        string dist_str = "";
                                        if (Components.VisualsComponent.DrawTextDist.Enabled)
                                        {
                                            double playerDist = GetDistance3D(myPos, headpos);
                                            dist_str = $"[{playerDist.ToString("0.0")}]"; //only 1 demical number after the dot
                                        }
                                        if (myTeam != playerTeam)
                                        {
                                            if (Components.VisualsComponent.DrawTheVisuals.Enabled)
                                            {
                                                Renderer.DrawFPSBox(vScreen_head, vScreen_foot, Components.VisualsComponent.EnemiesColor.Color, (stanceFlt == 4.50f ? BoxStance.standing : BoxStance.crouching), Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled,Components.VisualsComponent.DrawBox.Enabled,health,Components.VisualsComponent.DrawBoxHP.Enabled ? 100 : 0,armor,Components.VisualsComponent.DrawBoxAR.Enabled ? 100 : 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, Components.VisualsComponent.DrawTextName.Enabled ? playerName : "", string.Empty, string.Empty, string.Empty);
                                            }
                                        }
                                        else
                                        {
                                            if (Components.VisualsComponent.DrawTheVisuals.Enabled)
                                            {
                                                if (Components.VisualsComponent.DrawAlliesEsp.Enabled)
                                                {
                                                    Renderer.DrawFPSBox(vScreen_head, vScreen_foot, Components.VisualsComponent.AlliesColor.Color, (stanceFlt == 4.50f ? BoxStance.standing : BoxStance.crouching), Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled, Components.VisualsComponent.DrawBox.Enabled, health, Components.VisualsComponent.DrawBoxHP.Enabled ? 100 : 0, armor, Components.VisualsComponent.DrawBoxAR.Enabled ? 100 : 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, Components.VisualsComponent.DrawTextName.Enabled ? playerName : "", string.Empty, string.Empty, string.Empty);
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
