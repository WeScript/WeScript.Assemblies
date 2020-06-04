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
        public static float M_PI_F = (180.0f / Convert.ToSingle(System.Math.PI));
        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw
        public static Vector2 GameCenterPos = new Vector2(0, 0); //for crosshair and aim
        public static Vector2 AimTarg2D = new Vector2(0, 0); //for aimbot
        public static Vector3 AimTarg3D = new Vector3(0, 0, 0);
        public static IntPtr client_panorama = IntPtr.Zero;
        public static IntPtr client_panorama_size = IntPtr.Zero;
        public static IntPtr engine_dll = IntPtr.Zero;
        public static IntPtr engine_dll_size = IntPtr.Zero;
        public static IntPtr dwViewMatrix_Offs = IntPtr.Zero;
        public static IntPtr dwEntityList_Offs = IntPtr.Zero;
        public static IntPtr dwLocalPlayer_Offs = IntPtr.Zero;
        public static IntPtr dwSetViewAng_Addr = IntPtr.Zero;
        public static IntPtr dwSetViewAng_Offs = IntPtr.Zero;

        public static int WM_KEYDOWN = 0x0100;
        public static int WM_KEYUP = 0x0101;
        public static int myHPBefore = 0;
        public static bool shouldpostmsg = false;

        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }
        public static Menu AimbotMenu { get; private set; }
        public static Menu MiscMenu { get; private set; }

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
            public static class AimbotComponent
            {
                public static readonly MenuBool AimGlobalBool = new MenuBool("enableaim", "Enable Aimbot Features", true);
                public static readonly MenuKeyBind AimKey = new MenuKeyBind("aimkey", "Aimbot HotKey (HOLD)", VirtualKeyCode.LeftMouse, KeybindType.Hold, false);
                public static readonly MenuList AimType = new MenuList("aimtype", "Aimbot Type", new List<string>() { "Direct Engine ViewAngles", "Real Mouse Movement" }, 0);
                public static readonly MenuList AimSpot = new MenuList("aimspot", "Aimbot Spot", new List<string>() { "Aim at their Head", "Aim at their Body" }, 0);
                public static readonly MenuBool AIMRC = new MenuBool("aimrecoilcompens", "Compensate weapon recoil while aimbotting?", true);
                public static readonly MenuBool DrawRecoil = new MenuBool("drawrecoilpattern", "Draw Weapon Recoil Crosshair", true);
                public static readonly MenuColor RecoilColor = new MenuColor("recoilcolor", "Recoil Compensation Color", new SharpDX.Color(255, 0, 0, 255));
                public static readonly MenuBool AimAtEveryone = new MenuBool("aimeveryone", "Aim At Everyone (even teammates)", false);
                public static readonly MenuSlider AimSpeed = new MenuSlider("aimspeed", "Aimbot Speed %", 12, 1, 100);
                public static readonly MenuBool DrawAimSpot = new MenuBool("drawaimspot", "Draw Aimbot Spot", true);
                public static readonly MenuBool DrawAimTarget = new MenuBool("drawaimtarget", "Draw Aimbot Current Target", true);
                public static readonly MenuColor AimTargetColor = new MenuColor("aimtargetcolor", "Target Color", new SharpDX.Color(0x1F, 0xBE, 0xD6, 255));
                public static readonly MenuBool DrawAimFov = new MenuBool("drawaimfov", "Draw Aimbot FOV Circle", true);
                public static readonly MenuColor AimFovColor = new MenuColor("aimfovcolor", "FOV Color", new SharpDX.Color(255, 255, 255, 30));
                public static readonly MenuSlider AimFov = new MenuSlider("aimfov", "Aimbot FOV", 100, 4, 1000);
            }
            public static class MiscComponent
            {
                public static readonly MenuBool SupportInChat = new MenuBool("supportinchat", "Support WeScript.app by promoting it in chat to your teammates?", true);
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

            AimbotMenu = new Menu("aimbotmenu", "Aimbot Menu")
            {
                Components.AimbotComponent.AimGlobalBool,
                Components.AimbotComponent.AimKey,
                Components.AimbotComponent.AimType,
                Components.AimbotComponent.AimSpot,
                Components.AimbotComponent.AIMRC,
                Components.AimbotComponent.DrawRecoil,
                Components.AimbotComponent.RecoilColor,
                Components.AimbotComponent.AimAtEveryone,
                Components.AimbotComponent.AimSpeed,
                Components.AimbotComponent.DrawAimSpot,
                Components.AimbotComponent.DrawAimTarget,
                Components.AimbotComponent.DrawAimFov,
                Components.AimbotComponent.AimFovColor,
                Components.AimbotComponent.AimFov,
            };

            MiscMenu = new Menu("miscmenu", "Misc Menu")
            {
                Components.MiscComponent.SupportInChat,
            };

            RootMenu = new Menu("csgoexample", "WeScript.app CSGO Example Assembly", true)
            {
                Components.MainAssemblyToggle.SetToolTip("The magical boolean which completely disables/enables the assembly!"),
                VisualsMenu,
                AimbotMenu,
                MiscMenu,
            };
            RootMenu.Attach();
        }
        private static double GetDistance3D(Vector3 myPos, Vector3 enemyPos)
        {
            Vector3 vector = new Vector3(myPos.X - enemyPos.X, myPos.Y - enemyPos.Y, myPos.Z - enemyPos.Z);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        }

        private static double GetDistance2D(Vector2 pos1, Vector2 pos2)
        {
            Vector2 vector = new Vector2(pos1.X - pos2.X, pos1.Y - pos2.Y);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }

        private static Vector3 ReadBonePos(IntPtr playerPtr, int boneIDX)
        {
            Vector3 targetVec = new Vector3(0, 0, 0);
            var BoneMatrixPtr = Memory.ReadPointer(processHandle, (IntPtr)(playerPtr.ToInt64() + 0x26A8), isWow64Process); //m_dwBoneMatrix
            if (BoneMatrixPtr != IntPtr.Zero)
            {
                targetVec.X = Memory.ReadFloat(processHandle, (IntPtr)(BoneMatrixPtr.ToInt64() + 0x30 * boneIDX + 0x0C));
                targetVec.Y = Memory.ReadFloat(processHandle, (IntPtr)(BoneMatrixPtr.ToInt64() + 0x30 * boneIDX + 0x1C));
                targetVec.Z = Memory.ReadFloat(processHandle, (IntPtr)(BoneMatrixPtr.ToInt64() + 0x30 * boneIDX + 0x2C));
            }
            return targetVec;
        }

        public static Vector3 ClampAngle(Vector3 angle)
        {
            while (angle.Y > 180) angle.Y -= 360;
            while (angle.Y < -180) angle.Y += 360;

            if (angle.X > 89.0f) angle.X = 89.0f;
            if (angle.X < -89.0f) angle.X = -89.0f;

            angle.Z = 0f;

            return angle;
        }

        public static Vector3 NormalizeAngle(Vector3 angle)
        {
            while (angle.X < -180.0f) angle.X += 360.0f;
            while (angle.X > 180.0f) angle.X -= 360.0f;

            while (angle.Y < -180.0f) angle.Y += 360.0f;
            while (angle.Y > 180.0f) angle.Y -= 360.0f;

            while (angle.Z < -180.0f) angle.Z += 360.0f;
            while (angle.Z > 180.0f) angle.Z -= 360.0f;

            return angle;
        }

        public static Vector3 CalcAngle(Vector3 playerPosition, Vector3 enemyPosition, Vector3 aimPunch, Vector3 vecView, float yawRecoilReductionFactory, float pitchRecoilReductionFactor)
        {
            Vector3 delta = new Vector3(playerPosition.X - enemyPosition.X, playerPosition.Y - enemyPosition.Y, (playerPosition.Z + vecView.Z) - enemyPosition.Z);

            Vector3 tmp = Vector3.Zero;
            tmp.X = Convert.ToSingle(System.Math.Atan(delta.Z / System.Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y))) * 57.295779513082f - aimPunch.X * yawRecoilReductionFactory;
            tmp.Y = Convert.ToSingle(System.Math.Atan(delta.Y / delta.X)) * M_PI_F - aimPunch.Y * pitchRecoilReductionFactor;
            tmp.Z = 0;

            if (delta.X >= 0.0) tmp.Y += 180f;

            tmp = NormalizeAngle(tmp);
            tmp = ClampAngle(tmp);

            return tmp;
        }

        public static void SendChatMessageToTeam(IntPtr gameWindow)
        {
            Input.PostMessageWS(gameWindow, WM_KEYDOWN, (int)VirtualKeyCode.U, (IntPtr)(Input.MapVirtualKeyWS((uint)VirtualKeyCode.U, 0) << 16)); //keydown U
            Input.PostMessageWS(gameWindow, WM_KEYUP, (int)VirtualKeyCode.U, (IntPtr)(Input.MapVirtualKeyWS((uint)VirtualKeyCode.U, 0) << 16)); //keyup U
            Input.SleepWS(100);
            Input.SendString("I am using WWW.WESCRIPT.APP to carry my team!");
            Input.SleepWS(50);
            Input.KeyPress(VirtualKeyCode.Enter);
        }


        static void Main(string[] args)
        {
            Console.WriteLine("WeScript.app CSGO Example Assembly Loaded! (last update [04.06.2020])");

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
                    GameCenterPos = new Vector2(wndSize.X / 2 + wndMargins.X, wndSize.Y / 2 + wndMargins.Y); //even if the game is windowed, calculate perfectly it's "center" for aim or crosshair
                    isOverlayOnTop = Overlay.IsOnTop();

                    if (client_panorama == IntPtr.Zero) //if the dll is still null
                    {
                        client_panorama = Memory.GetModule(processHandle, "client.dll", isWow64Process); //attempt to find the module (if it's loaded)
                    }
                    else
                    {
                        if (client_panorama_size == IntPtr.Zero) //dll got loaded, check if size is zero
                        {
                            client_panorama_size = Memory.GetModuleSize(processHandle, "client.dll", isWow64Process); //get module size
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
                    if (engine_dll == IntPtr.Zero)
                    {
                        engine_dll = Memory.GetModule(processHandle, "engine.dll", isWow64Process);
                    }
                    else
                    {
                        if (engine_dll_size == IntPtr.Zero)
                        {
                            engine_dll_size = Memory.GetModuleSize(processHandle, "engine.dll", isWow64Process);
                        }
                        else
                        {
                            if (dwSetViewAng_Addr == IntPtr.Zero)
                            {
                                dwSetViewAng_Addr = Memory.FindSignature(processHandle, engine_dll, engine_dll_size, "F3 0F 11 80 ? ? ? ? D9 46 04 D9 05", -0x4);
                            }
                            if (dwSetViewAng_Offs == IntPtr.Zero)
                            {
                                dwSetViewAng_Offs = Memory.FindSignature(processHandle, engine_dll, engine_dll_size, "F3 0F 11 80 ? ? ? ? D9 46 04 D9 05", 0x4);
                            }
                            if (Components.MiscComponent.SupportInChat.Enabled)
                            {
                                if (isGameOnTop)
                                {
                                    if (shouldpostmsg)
                                    {
                                        shouldpostmsg = false;
                                        SendChatMessageToTeam(wndHnd);
                                    }
                                }
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
                    engine_dll = IntPtr.Zero;
                    client_panorama_size = IntPtr.Zero;
                    engine_dll_size = IntPtr.Zero;
                    dwViewMatrix_Offs = IntPtr.Zero;
                    dwEntityList_Offs = IntPtr.Zero;
                    dwLocalPlayer_Offs = IntPtr.Zero;
                    dwSetViewAng_Addr = IntPtr.Zero;
                    dwSetViewAng_Offs = IntPtr.Zero;
                }
            }
        }

        private static void OnRenderer(int fps, EventArgs args)
        {
            if (!gameProcessExists) return; //process is dead, don't bother drawing
            if ((!isGameOnTop) && (!isOverlayOnTop)) return; //if game and overlay are not on top, don't draw
            if (!Components.MainAssemblyToggle.Enabled) return; //main menu boolean to toggle the cheat on or off

            double fClosestPos = 999999;
            AimTarg2D = new Vector2(0, 0);
            AimTarg3D = new Vector3(0, 0, 0);

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
                            if (Components.MiscComponent.SupportInChat.Enabled)
                            {
                                var myHP = Memory.ReadInt32(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x100));
                                if (myHP == 100) //we're alive rn
                                {
                                    if (myHPBefore == 0) //we were dead the prev frame
                                    {
                                        myHPBefore = myHP;
                                        shouldpostmsg = true;
                                    }
                                }
                                else
                                {
                                    if (myHP == 0)
                                    {
                                        myHPBefore = 0;
                                    }
                                }
                            }
                            var myPos = Memory.ReadVector3(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x138));
                            var myTeam = Memory.ReadByte(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0xF4));
                            var myAngles = Memory.ReadVector3(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x31D8)); //m_thirdPersonViewAngles
                            var myEyePos = Memory.ReadVector3(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x108)); //m_vecViewOffset
                            var myPunchAngles = Memory.ReadVector3(processHandle, (IntPtr)(LocalPlayer.ToInt64() + 0x302C)); //m_aimPunchAngle 
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
                                    var isenemybool = (myTeam != m_iTeamNum);

                                    if ((m_iHealth > 0) && (bDormant == false)) //entity is valid? (should add more checks)
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
                                                    if ((!Components.VisualsComponent.DrawAlliesEsp.Enabled) && (!isenemybool)) continue; //skil allies
                                                    Renderer.DrawFPSBox(vScreen_head, vScreen_foot, (m_iTeamNum == 3) ? Components.VisualsComponent.CTColor.Color : Components.VisualsComponent.TRColor.Color, (f_modelHeight == 54.0f) ? BoxStance.crouching : BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled, Components.VisualsComponent.DrawBox.Enabled, m_iHealth, Components.VisualsComponent.DrawBoxHP.Enabled ? 100 : 0, 0, 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, string.Empty, string.Empty, string.Empty, string.Empty);
                                                }
                                            }
                                        }
                                        if (Components.AimbotComponent.AimGlobalBool.Enabled)
                                        {
                                            if (!Components.AimbotComponent.AimAtEveryone.Enabled)
                                            {
                                                if (!isenemybool) continue; //skip allies
                                            }
                                            Vector3 targetVec = new Vector3(0, 0, 0);
                                            switch (Components.AimbotComponent.AimSpot.Value)
                                            {
                                                case 0: //head
                                                    {
                                                        targetVec = ReadBonePos(entityAddr, 8);
                                                    }
                                                    break;
                                                case 1: //body
                                                    {
                                                        targetVec = ReadBonePos(entityAddr, 0);
                                                    }
                                                    break;
                                                default: //ignore default case, should never occur
                                                    break;
                                            }
                                            Vector2 vScreen_aim = new Vector2(0, 0);
                                            if (Renderer.WorldToScreen(targetVec, out vScreen_aim, matrix, wndMargins, wndSize, W2SType.TypeD3D9)) //our aimpoint is on screen
                                            {
                                                if (Components.AimbotComponent.DrawAimSpot.Enabled)
                                                {
                                                    Renderer.DrawFilledRect(vScreen_aim.X - 1, vScreen_aim.Y - 1, 2, 2, new Color(255, 255, 255)); //lazy to implement aimspotcolor
                                                }
                                                var AimDist2D = GetDistance2D(vScreen_aim, GameCenterPos);
                                                if (Components.AimbotComponent.AimFov.Value < AimDist2D) continue; //ignore anything outside our fov
                                                if (AimDist2D < fClosestPos)
                                                {
                                                    fClosestPos = AimDist2D;
                                                    AimTarg2D = vScreen_aim;
                                                    AimTarg3D = targetVec;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (Components.AimbotComponent.AimGlobalBool.Enabled)
                            {
                                if (Components.AimbotComponent.DrawAimFov.Enabled) //draw fov circle
                                {
                                    Renderer.DrawCircle(GameCenterPos, Components.AimbotComponent.AimFov.Value, Components.AimbotComponent.AimFovColor.Color);
                                }

                                var dx = (GameCenterPos.X * 2) / 90;
                                var dy = (GameCenterPos.Y * 2) / 90;

                                var rx = GameCenterPos.X - (dx * ((myPunchAngles.Y)));
                                var ry = GameCenterPos.Y + (dy * ((myPunchAngles.X)));
                                if (Components.AimbotComponent.DrawRecoil.Enabled)
                                {
                                    Renderer.DrawFilledRect(rx - 1, ry - 1, 2, 2, Components.AimbotComponent.RecoilColor.Color);
                                }

                                if ((AimTarg2D.X != 0) && (AimTarg2D.Y != 0))//check just in case if we have aimtarg
                                {
                                    if (Components.AimbotComponent.DrawAimTarget.Enabled) //draw aim target
                                    {
                                        Renderer.DrawRect(AimTarg2D.X - 3, AimTarg2D.Y - 3, 6, 6, Components.AimbotComponent.AimTargetColor.Color);
                                    }
                                    if (Components.AimbotComponent.AimKey.Enabled)
                                    {

                                        switch (Components.AimbotComponent.AimType.Value)
                                        {
                                            case 0: //engine viewangles
                                                {
                                                    if (dwSetViewAng_Addr != IntPtr.Zero)
                                                    {
                                                        var setViewAngles = Memory.ReadPointer(processHandle, dwSetViewAng_Addr, isWow64Process);
                                                        if ((setViewAngles != IntPtr.Zero) & (dwSetViewAng_Offs != IntPtr.Zero))
                                                        {
                                                            float recoilCompens = Components.AimbotComponent.AIMRC.Enabled ? 2.0f : 0.0f;
                                                            var newAng = CalcAngle(myPos, AimTarg3D, myPunchAngles, myEyePos, recoilCompens, recoilCompens);
                                                            if (Components.AimbotComponent.AimSpeed.Value < 100) //smoothing only below 100%
                                                            {
                                                                float aimsmooth_ = Components.AimbotComponent.AimSpeed.Value * 0.01f;
                                                                var diff = newAng - myAngles;
                                                                diff = NormalizeAngle(diff);
                                                                diff = ClampAngle(diff);
                                                                if (diff.X > aimsmooth_)
                                                                {
                                                                    newAng.X = myAngles.X + aimsmooth_;
                                                                }
                                                                if (diff.X < -aimsmooth_)
                                                                {
                                                                    newAng.X = myAngles.X - aimsmooth_;
                                                                }
                                                                if (diff.Y > aimsmooth_)
                                                                {
                                                                    newAng.Y = myAngles.Y + aimsmooth_;
                                                                }
                                                                if (diff.Y < -aimsmooth_)
                                                                {
                                                                    newAng.Y = myAngles.Y - aimsmooth_;
                                                                }
                                                                newAng = ClampAngle(newAng); //just in case?
                                                            }
                                                            Memory.WriteVector3(processHandle, (IntPtr)(setViewAngles.ToInt64() + dwSetViewAng_Offs.ToInt64()), newAng);
                                                        }
                                                    }
                                                }
                                                break;
                                            case 1: //mouse event
                                                {

                                                    double DistX = 0;
                                                    double DistY = 0;
                                                    if (Components.AimbotComponent.AIMRC.Enabled)
                                                    {
                                                        DistX = (AimTarg2D.X) - rx;
                                                        DistY = (AimTarg2D.Y) - ry;
                                                    }
                                                    else
                                                    {
                                                        DistX = (AimTarg2D.X) - GameCenterPos.X;
                                                        DistY = (AimTarg2D.Y) - GameCenterPos.Y;
                                                    }
                                                    double slowDistX = DistX / (1.0f + (Math.Abs(DistX) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    double slowDistY = DistY / (1.0f + (Math.Abs(DistY) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    Input.mouse_eventWS(MouseEventFlags.MOVE, (int)slowDistX, (int)slowDistY, MouseEventDataXButtons.NONE, IntPtr.Zero);
                                                }
                                                break;
                                            default: //ignore default case, should never occur
                                                break;
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
