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

namespace ViewProjectionMatrixFinder
{
    class Program
    {

        public static IntPtr procHnd = IntPtr.Zero;
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF;
        public static bool is64bit = false;
        public static bool gameProcessExists = false;
        public static bool isGameOnTop = false;
        public static Vector2 wndMargins = new Vector2(0, 0);
        public static Vector2 wndSize = new Vector2(0, 0);
        public static ulong[] returnedAddresses = new ulong[1000000];
        public static Matrix[] returnedMatrixes = new Matrix[1000000];
        public static int matrixesFnd = 0;
        public static string GameWindowName = "Counter-Strike: Global Offensive";
        public static string GameModuleName = "client_panorama.dll";
        public static string CurrentStatus = "";
        public static IntPtr GameModuleBase = IntPtr.Zero;
        public static IntPtr GameModuleSize = IntPtr.Zero;
        public static Vector3 vecToSearchFor;
        public static bool engageScanning;
        public static bool engageStatic;
        public static bool engageDynamic;
        public static uint leftamount = 0;


        public static Menu RootMenu { get; private set; }
        public static Menu SettingsMenu { get; private set; }


        class Components
        {
            public static class SettingsComponent
            {
                public static readonly MenuBool DrawHelperStrings = new MenuBool("helperstrings", "Draw helper strings at top left corner", true);
                public static readonly MenuSlider DrawHelperSettingSize = new MenuSlider("helperfontsize", "Font Size of Helper text", 28, 12, 30);
                public static readonly MenuKeyBind ResetScanning = new MenuKeyBind("resetscans", "Reset/Start Scanning", VirtualKeyCode.Delete, KeybindType.Toggle, false);
                public static readonly MenuKeyBind CleanStatic = new MenuKeyBind("cleanstatic", "Clean from static/invalid matrix", VirtualKeyCode.Home, KeybindType.Toggle, false);
                public static readonly MenuKeyBind CleanDynamic = new MenuKeyBind("cleandynamic", "Clean from dynamic matrix while stationary", VirtualKeyCode.End, KeybindType.Toggle, false);
                public static readonly MenuList W2SType = new MenuList("w2stype", "Choose WorldToScreen Type", new List<string>() { "Type.OGL", "Type.D3D9", "Type.D3D11"  }, 0);
                public static readonly MenuSlider VecSearchX = new MenuSlider("vecsliderx", "Vector to search pos.[X]", 0, -1000, 1000);
                public static readonly MenuSlider VecSearchY = new MenuSlider("vecslidery", "Vector to search pos.[Y]", 0, -1000, 1000);
                public static readonly MenuSlider VecSearchZ = new MenuSlider("vecsliderz", "Vector to search pos.[Z]", 0, -1000, 1000);
                public static readonly MenuList DrawingType = new MenuList("drawtype", "Drawing found addresses on screen", new List<string>() { "Draw as address", "Draw as dots"  }, 0);
                public static readonly MenuBool DrawStackedAddr = new MenuBool("liststackedones", "Draw a box, in which you get a list of addresses unstacked", true);
            }
        }

        public static void InitializeMenu()
        {
            SettingsMenu = new Menu("settingsmenu", "Settings Menu")
            {
                Components.SettingsComponent.DrawHelperStrings,
                Components.SettingsComponent.DrawHelperSettingSize,
                Components.SettingsComponent.ResetScanning,
                Components.SettingsComponent.CleanStatic,
                Components.SettingsComponent.CleanDynamic,
                Components.SettingsComponent.W2SType,
                Components.SettingsComponent.VecSearchX,
                Components.SettingsComponent.VecSearchY,
                Components.SettingsComponent.VecSearchZ,
                Components.SettingsComponent.DrawingType,
            };


            RootMenu = new Menu("viewprojectionmatrixdev", "WeScript.app [ViewProjectionMatrixFinder] Assembly", true)
            {
                SettingsMenu,
            };
            RootMenu.Attach();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("WeScript.app ViewProjectionMatrixFinder assembly for developers!");
            Console.WriteLine("Helps you in finding the matrix for a working WorldToScreen function.");

            InitializeMenu();
            engageScanning = Components.SettingsComponent.ResetScanning.Enabled;
            engageStatic = Components.SettingsComponent.CleanStatic.Enabled;
            engageDynamic = Components.SettingsComponent.CleanDynamic.Enabled;
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }


        private static void OnTick(int counter, EventArgs args)
        {
            if (procHnd == IntPtr.Zero) 
            {
                var wndHnd = Memory.FindWindowName(GameWindowName); 
                if (wndHnd != IntPtr.Zero) 
                {
                    var gamePID = Memory.GetPIDFromHWND(wndHnd); 
                    if (gamePID > 0) 
                    {
                        procHnd = Memory.OpenProcess(PROCESS_ALL_ACCESS, gamePID); 
                        if (procHnd != IntPtr.Zero)
                        {
                            is64bit = Memory.IsProcess64Bit(procHnd);
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowName(GameWindowName);
                if (wndHnd != IntPtr.Zero) 
                {
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);

                    if (GameModuleBase == IntPtr.Zero) //if the dll is still null
                    {
                        GameModuleBase = Memory.GetModule(procHnd, GameModuleName, is64bit); //attempt to find the module (if it's loaded)
                    }
                    else
                    {
                        if (GameModuleSize == IntPtr.Zero) //dll got loaded, check if size is zero
                        {
                            GameModuleSize = Memory.GetModuleSize(procHnd, GameModuleName, is64bit); //get module size
                        }
                        else
                        {
                            //Console.WriteLine("all good");
                            vecToSearchFor = new Vector3(Components.SettingsComponent.VecSearchX.Value, Components.SettingsComponent.VecSearchY.Value, Components.SettingsComponent.VecSearchZ.Value);
                            //vecToSearchFor = new Vector3(-2940.042725f, -219.2802734f, 53.91707611f);
                            if (engageScanning != Components.SettingsComponent.ResetScanning.Enabled)
                            {
                                engageScanning = Components.SettingsComponent.ResetScanning.Enabled;
                                matrixesFnd = 0;
                                CurrentStatus = "Starting the initial scan...";
                                Console.WriteLine("");
                                Console.WriteLine("===========================");
                                Console.WriteLine($"{CurrentStatus} GameModuleBase:{GameModuleBase.ToString("X")} GameModuleSize:{GameModuleSize.ToString("X")}");
                                matrixesFnd = Memory.FindPossibleMatrix(procHnd, GameModuleBase, GameModuleSize, vecToSearchFor, out returnedAddresses, Components.SettingsComponent.W2SType.Value);
                                //matrixesFnd = Memory.FindPossibleMatrix(procHnd, IntPtr.Zero, (IntPtr)0x7FFFFFFF, vecToSearchFor, out returnedAddresses, Components.SettingsComponent.W2SType.Value);
                                Console.WriteLine($"Total possible matrix amount found: {matrixesFnd.ToString()}");
                                for (uint i = 0; i <= matrixesFnd; i++)
                                {
                                    returnedMatrixes[i] = Memory.ReadMatrix(procHnd, (IntPtr)returnedAddresses[i]);
                                }
                                leftamount = (uint)matrixesFnd;
                                Console.WriteLine("Reading the matrixes data completed...");
                                Console.WriteLine("===========================");
                                Console.WriteLine("");
                                CurrentStatus = "";
                            }
                            if (engageStatic != Components.SettingsComponent.CleanStatic.Enabled)
                            {
                                engageStatic = Components.SettingsComponent.CleanStatic.Enabled;
                                CurrentStatus = "Attempting to clean from invalid/static matrixes... (Hope you didn't forget to move your camera a bit)";
                                Console.WriteLine("");
                                Console.WriteLine("===========================");
                                Console.WriteLine(CurrentStatus);
                                leftamount = 0;
                                for (uint i = 0; i <= matrixesFnd; i++)
                                {
                                    if (returnedAddresses[i] != 0)
                                    {
                                        var tempMatrix = Memory.ReadMatrix(procHnd, (IntPtr)returnedAddresses[i]);
                                        if ((tempMatrix.M11 == returnedMatrixes[i].M11) && (tempMatrix.M12 == returnedMatrixes[i].M12) && (tempMatrix.M13 == returnedMatrixes[i].M13) && (tempMatrix.M14 == returnedMatrixes[i].M14)
                                               && (tempMatrix.M21 == returnedMatrixes[i].M21) && (tempMatrix.M22 == returnedMatrixes[i].M22) && (tempMatrix.M23 == returnedMatrixes[i].M23) && (tempMatrix.M24 == returnedMatrixes[i].M24)
                                               && (tempMatrix.M31 == returnedMatrixes[i].M31) && (tempMatrix.M32 == returnedMatrixes[i].M32) && (tempMatrix.M33 == returnedMatrixes[i].M33) && (tempMatrix.M34 == returnedMatrixes[i].M34)
                                               && (tempMatrix.M41 == returnedMatrixes[i].M41) && (tempMatrix.M42 == returnedMatrixes[i].M42) && (tempMatrix.M43 == returnedMatrixes[i].M43) && (tempMatrix.M44 == returnedMatrixes[i].M44))
                                        {
                                            returnedAddresses[i] = 0;
                                        }
                                    }
                                }
                                for (uint i = 0; i <= matrixesFnd; i++)
                                {
                                    if (returnedAddresses[i] != 0)
                                    {
                                        leftamount++;
                                        returnedMatrixes[i] = Memory.ReadMatrix(procHnd, (IntPtr)returnedAddresses[i]);
                                    }
                                }
                                Console.WriteLine("Cleaning from static/invalid completed...");
                                Console.WriteLine($"Total possible matrix left: {leftamount.ToString()}");
                                Console.WriteLine("===========================");
                                Console.WriteLine("");
                                CurrentStatus = "";
                            }
                            if (engageDynamic != Components.SettingsComponent.CleanDynamic.Enabled)
                            {
                                engageDynamic = Components.SettingsComponent.CleanDynamic.Enabled;
                                CurrentStatus = "Attempting to clean from constantly changing matrixes while camera is stationary...";
                                Console.WriteLine("");
                                Console.WriteLine("===========================");
                                Console.WriteLine(CurrentStatus);
                                leftamount = 0;
                                for (uint i = 0; i <= matrixesFnd; i++)
                                {
                                    if (returnedAddresses[i] != 0)
                                    {
                                        var tempMatrix = Memory.ReadMatrix(procHnd, (IntPtr)returnedAddresses[i]);
                                        if ((tempMatrix.M11 != returnedMatrixes[i].M11) || (tempMatrix.M12 != returnedMatrixes[i].M12) || (tempMatrix.M13 != returnedMatrixes[i].M13) || (tempMatrix.M14 != returnedMatrixes[i].M14)
                                               && (tempMatrix.M21 != returnedMatrixes[i].M21) || (tempMatrix.M22 != returnedMatrixes[i].M22) || (tempMatrix.M23 != returnedMatrixes[i].M23) || (tempMatrix.M24 != returnedMatrixes[i].M24)
                                               && (tempMatrix.M31 != returnedMatrixes[i].M31) || (tempMatrix.M32 != returnedMatrixes[i].M32) || (tempMatrix.M33 != returnedMatrixes[i].M33) || (tempMatrix.M34 != returnedMatrixes[i].M34)
                                               && (tempMatrix.M41 != returnedMatrixes[i].M41) || (tempMatrix.M42 != returnedMatrixes[i].M42) || (tempMatrix.M43 != returnedMatrixes[i].M43) || (tempMatrix.M44 != returnedMatrixes[i].M44))
                                        {
                                            returnedAddresses[i] = 0;
                                        }
                                    }
                                }
                                for (uint i = 0; i <= matrixesFnd; i++)
                                {
                                    if (returnedAddresses[i] != 0)
                                    {
                                        leftamount++;
                                        returnedMatrixes[i] = Memory.ReadMatrix(procHnd, (IntPtr)returnedAddresses[i]);
                                    }
                                }
                                Console.WriteLine("Cleaning from constantly changing matrixes completed...");
                                Console.WriteLine($"Total possible matrix left: {leftamount.ToString()}");
                                Console.WriteLine("===========================");
                                Console.WriteLine("");
                                CurrentStatus = "";
                            }
                        }
                    }
                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(procHnd); //close the handle to avoid leaks
                    procHnd = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;
                    matrixesFnd = 0;

                    //clear your offsets, modules
                    GameModuleBase = IntPtr.Zero;
                    GameModuleSize = IntPtr.Zero;
                }
            }
        }


        private static void OnRenderer(int fps, EventArgs args)
        {

            if (!gameProcessExists) return;
            if (!isGameOnTop) return;
            if (Components.SettingsComponent.DrawHelperStrings.Enabled)
            {
                Renderer.DrawText($"Current Status: {CurrentStatus}", new Vector2(100, 100), new Color(255, 255, 255), Components.SettingsComponent.DrawHelperSettingSize.Value);
                switch (Components.SettingsComponent.W2SType.Value)
                {
                    case 0: //OGL
                        {
                            Renderer.DrawText($"W2SType: Type.OpenGL", new Vector2(100, 120), new Color(255, 255, 255), Components.SettingsComponent.DrawHelperSettingSize.Value);
                        }
                        break;
                    case 1: //D3D9
                        {
                            Renderer.DrawText($"W2SType: Type.D3D9", new Vector2(100, 120), new Color(255, 255, 255), Components.SettingsComponent.DrawHelperSettingSize.Value);
                        }
                        break;
                    case 2: //D3D11
                        {
                            Renderer.DrawText($"W2SType: Type.D3D11", new Vector2(100, 120), new Color(255, 255, 255), Components.SettingsComponent.DrawHelperSettingSize.Value);
                        }
                        break;
                    default: //ignore default case, should never occur
                        break;
                }
                Renderer.DrawText($"Current VecSearch: {vecToSearchFor.ToString("0")}", new Vector2(100, 140), new Color(255, 255, 255), Components.SettingsComponent.DrawHelperSettingSize.Value);
                Renderer.DrawText($"Matrix found: {leftamount.ToString()}", new Vector2(100, 160), new Color(255, 255, 255), Components.SettingsComponent.DrawHelperSettingSize.Value);
            }
            
            if (Components.SettingsComponent.DrawStackedAddr.Enabled)
            {
                Renderer.DrawRect(wndMargins.X + wndSize.X / 4, wndMargins.Y + 100, 50, 50, new Color(255, 255, 255, 100));
            }


            if (leftamount > 0)
            {
                int j = 0;
                for (uint i = 0; i <= matrixesFnd; i++)
                {
                    if (returnedAddresses[i] != 0)
                    {
                        Vector2 vec2D = new Vector2(0, 0);
                        var matrix = Memory.ReadMatrix(procHnd, (IntPtr)returnedAddresses[i]);
                        if (Renderer.WorldToScreen(vecToSearchFor, out vec2D, (matrix), wndMargins, wndSize, (W2SType)Components.SettingsComponent.W2SType.Value))
                        {
                            if (Components.SettingsComponent.DrawingType.Value == 0)
                            {
                                Renderer.DrawText($"{returnedAddresses[i].ToString("X")}", vec2D.X, vec2D.Y, new Color(255, 255, 255, 100), 16, TextAlignment.lefted, false);
                            }
                            else
                            {
                                Renderer.DrawFilledRect(vec2D.X-2, vec2D.Y-2, 4,4, new Color(255, 0, 0));
                            }
                            
                            if ((vec2D.X > wndMargins.X + wndSize.X / 4) && (vec2D.Y > (wndMargins.Y + 100)) && (vec2D.X < (wndMargins.X + wndSize.X / 4 + 50)) && (vec2D.Y < (wndMargins.Y + 100 + 50)))
                            {
                                //it's inside box of truth
                                j += 20;
                                if (Components.SettingsComponent.DrawStackedAddr.Enabled)
                                {
                                    Renderer.DrawText($"{returnedAddresses[i].ToString("X")}", 100, 180 + j, Color.White, Components.SettingsComponent.DrawHelperSettingSize.Value, TextAlignment.lefted, true);
                                }  
                            }
                        }
                    }
                }
            }

        }

    }
}
