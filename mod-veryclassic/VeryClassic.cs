using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace GameMod
{
    class VeryClassicController : MonoBehaviour
    {
        private void OnApplicationFocus(bool focus)
        {
            if (focus)
            {
                VeryClassic.VeryClassicKeyFlush();
                GameManager.MaybeLockCursor();
            }
        }

        void OnGUI()
        {
            if (Event.current.isKey)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode != 0)
                {
                    //Debug.Log("key down " + Event.current.keyCode);
                    VeryClassic.VeryClassicKey((int)Event.current.keyCode, 1);
                }
                if (Event.current.type == EventType.KeyUp && Event.current.keyCode != 0)
                {
                    //Debug.Log("key up " + Event.current.keyCode);
                    VeryClassic.VeryClassicKey((int)Event.current.keyCode, 0);
                }
            }
        }
    }

    class VeryClassic
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string filename);

        [DllImport("VeryClassic.dll")]
        static extern int VeryClassicInit(IntPtr screen, SoundPlay3DHandler play);

        [DllImport("VeryClassic.dll")]
        static extern int VeryClassicFrame(int lastKey, int c, SoundPlay3DHandler play);

        [DllImport("VeryClassic.dll")]
        public static extern void VeryClassicKey(int uKeyCode, int pressed);

        [DllImport("VeryClassic.dll")]
        public static extern void VeryClassicKeyFlush();

        [DllImport("VeryClassic.dll")]
        static extern int VeryClassicGameSoundCount(out int datalen);

        [DllImport("VeryClassic.dll")]
        static extern int VeryClassicGameSoundLoad(byte[] buf, int[] ofs, int[] len);

        [DllImport("VeryClassic.dll")]
        static extern int VeryClassicSettings(int difficulty, int autolevel, int dvolume, int mvolume, int level_num);

        [DllImport("VeryClassic.dll")]
        static extern int VeryClassicSetControls(uint buttons,
                float cc_mouse_raw_x, float cc_mouse_raw_y,
                float cc_turn_vec_x, float cc_turn_vec_y, float cc_turn_vec_z,
                float cc_move_vec_x, float cc_move_vec_y, float cc_move_vec_z,
                int cc_view_map, int cc_rear_view);
                
        public static string MissionName = "VeryClassicMission";
        public static string[] LevelNames = new[] { "LUNAR OUTPOST", "LUNAR SCILAB", "LUNAR MILITARY BASE",
                    "VENUS ATMOSPHERIC LAB", "VENUS NICKEL-IRON MINE",
                    "MERCURY SOLAR LAB", "MERCURY MILITARY HQ"};
        public static Texture2D m_main_texture;
        static byte[] m_screen_data;
        static GCHandle m_screen_handle;
        //static int shift;
        static AudioSource[] asrcs = new AudioSource[32];
        private delegate void SoundPlay3DHandler(int sndnum, int angle, int volume);

        private static SoundPlay3DHandler play;


        static private byte[] sndbuf;
        static private int[] sndofs, sndlen;
        static Dictionary<int, ClassicClip> clips = new Dictionary<int, ClassicClip>();
        public static bool IsDone;
        static GameObject camGO;
        static GameObject orgCamGO;

        class ClassicClip
        {
            public int position = 0;
            public int samplerate = 11025;
            //public byte[] buf;
            public AudioClip clip;

            public ClassicClip(int sndnum)
            {
                int len = sndlen[sndnum];
                if (len == 0)
                    return;
                clip = AudioClip.Create("#" + sndnum, len, 1, samplerate, false);
                float[] fdata = new float[len];
                int p = sndofs[sndnum];
                for (int i = 0; i < len; i++)
                    //fdata[i] = (buf[i] - 128) / 128f;
                    fdata[i] = (sndbuf[p + i] - 128) / 128f;
                clip.SetData(fdata, 0);
                //, OnAudioRead, OnAudioSetPosition);
            }

            /*
            void OnAudioRead(float[] data)
            {
                int count = 0;
                while (count < data.Length)
                {
                    data[count] = (buf[position] - 128) / 128f;
                    position++;
                    count++;
                }
            }

            void OnAudioSetPosition(int newPosition)
            {
                position = newPosition;
            }
            */
        }

        static int asrc_idx = 0;

        static int FindAudioSourceIndex()
        {
            int i = asrc_idx;
            int asrc_count = asrcs.Length;
            while (asrcs[i].isPlaying)
            {
                i = i == asrc_count - 1 ? 0 : i + 1;
                if (i == asrc_idx)
                    break;
            }
            asrc_idx = asrc_idx == asrc_count - 1 ? 0 : asrc_idx + 1;
            return i;
        }

        static float FixToFloat(int fix)
        {
            return fix / 65536f;
        }

        static void Play(int sndnum, float volume = 1, float panStereo = 0)
        {
            int idx = FindAudioSourceIndex();
            AudioSource asrc = asrcs[idx];
            asrc.volume = volume;
            asrc.panStereo = panStereo;
            asrc.PlayOneShot(GetClip(sndnum));
            //asrcNum[idx] = sndnum;
        }

        static void SoundPlay3D(int sndnum, int angle, int volume)
        {
            try
            {
                //Debug.Log("SoundPlay3D " + sndnum + " " + angle + " " + volume);
                if (volume < 10)
                    return;
                Play(sndnum, FixToFloat(volume), FixToFloat(angle * 2 - 65536));
                //Debug.Log("SoundPlay3DDone " + sndnum);
            }
            catch (Exception ex)
            {
                Debug.Log("SoundPlay3D failed " + ex);
            }
        }

        public static void Init(GameObject gameObject)
        {
            IsDone = false;
            m_main_texture = new Texture2D(320, 200, TextureFormat.RGBA32, false);
            m_main_texture.filterMode = FilterMode.Point;
            m_screen_data = new byte[320 * 200 * 4];
            m_screen_handle = GCHandle.Alloc(m_screen_data, GCHandleType.Pinned);

            var bCam = GameObject.Find("ui_camera");
            Debug.Log("bCam: " + bCam);
            if (bCam == null)
            {
                Debug.Log("briefing cam not found???");
            }
            else 
            {
                var bloom = bCam.gameObject.GetComponent<SENaturalBloomAndDirtyLens>();
                if (bloom != null)
                    bloom.enabled = false;
                var post = bCam.gameObject.GetComponent<PostProcessingBehaviour>();
                if (post != null)
                    post.enabled = false;
            }

            Camera mainCam = Camera.main;
            orgCamGO = mainCam.gameObject;
            orgCamGO.SetActive(false);
            camGO = new GameObject("ClassicCam");
            //camGO.transform.position = mainCam.transform.position;
            camGO.transform.parent = orgCamGO.transform.parent;
            // add distance for backside of cockpit
            camGO.transform.localPosition = mainCam.transform.localPosition;
            camGO.transform.localRotation = mainCam.transform.localRotation;
            //camGO.transform.localScale = mainCam.transform.localScale;
            var cam = camGO.AddComponent<Camera>();
            //cam.nearClipPlane = 0.01f;
            //cam.fieldOfView = mainCam.fieldOfView;
            //cam.depth = mainCam.depth;
            cam.orthographic = true;
            cam.orthographicSize = 0.44f;
            cam.allowHDR = false;
            cam.allowMSAA = false;

            for (var i = 0; i < asrcs.Length; i++)
                asrcs[i] = gameObject.AddComponent<AudioSource>();
            gameObject.AddComponent<VeryClassicController>();

            play = new SoundPlay3DHandler(SoundPlay3D);

            IntPtr x = LoadLibrary("VeryClassic.dll");
            Debug.Log("VeryClassicLib: " + x);
            VeryClassicSettings(GameplayManager.DifficultyLevel, MenuManager.opt_auto_leveling >= 2 ? 1 : 0,
                MenuManager.opt_volume_sfx, MenuManager.opt_volume_music, GameplayManager.Level.LevelNum + 1);
            int ret = VeryClassicInit(m_screen_handle.AddrOfPinnedObject(), play);
            Debug.Log("VeryClassicInit: " + ret);
#if false
            m_main_texture = new Texture2D(320, 200, TextureFormat.RGBA32, false);
            m_main_texture.filterMode = FilterMode.Point;
            //Debug.Log("name1: " + gameObject.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_MainTex").name);
            gameObject.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", m_main_texture);
            Debug.Log("name2: " + gameObject.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_MainTex").name);
#endif
            GameManager.MaybeLockCursor();
        }

        public static void Done()
        {
            GameManager.UnlockCursor();
            orgCamGO.SetActive(true);
            orgCamGO = null;
            UnityEngine.Object.Destroy(camGO);
            UIManager.DestroyAll(true);
            UIManager.ResetLayer(UIRenderLayer.IMAGE);
            CutsceneController.fade_in = false;
            UIManager.m_overlay_fade_away = true;
            UIManager.ShowCinematicBars(false, false);
            UIManager.SetOverlayAntiAlias(false);
            UnityEngine.Object.Destroy(MenuManager.m_cutscene_go);
            GameManager.m_audio.StopCutsceneSFX();
            MenuManager.m_cutscene_go = null;
            MenuManager.ChangeMenuState(MenuState.MAIN_MENU, false);

            Dictionary<string, LifetimeStatsSP> lifetime_stats_sp = (Dictionary<string, LifetimeStatsSP>)typeof(Scores).GetField("m_lifetime_stats_sp", AccessTools.all).GetValue(null);
            foreach (var levelName in VeryClassic.LevelNames)
            {
                var statsKey = MissionName + "/" + levelName;
                LifetimeStatsSP result = null;
                if (!lifetime_stats_sp.TryGetValue(statsKey, out result))
                    lifetime_stats_sp.Add(statsKey, new LifetimeStatsSP(1, 0));
            }
        }

        public static void Update()
        {
            if (IsDone)
                return;
            /*
            for (int i = 0; i < 320 * 200; i++)
            {
                m_screen_data[i * 4] = 0;
                m_screen_data[i * 4 + 1] = 255;
                m_screen_data[i * 4 + 2] = 0;
                m_screen_data[i * 4 + 3] = 255;
            }
            for (int i = 0; i < 200; i++)
                m_screen_data[(i * 320 + (i + shift) % 320) * 4] = 255;
            shift++;
            */

            Controls.MouseAimCache();
            Player pl = GameManager.m_local_player;
            pl.ClearCachedInput();
            pl.UpdateCachedButtons();
            pl.CacheButtons(Controls.m_input_count);
            //if (Controls.m_input_count[(int)CCInput.TURN_LEFT] != 0)
            //    Debug.Log("turn left: " + Controls.m_input_count[(int)CCInput.TURN_LEFT]);
            pl.c_player_ship.FixedUpdateReadCachedControls();
            uint buttons = pl.EncodePlayerButtonPresses();
            /*
            if (buttons != 0 && buttons != 21845)
                Debug.Log("buttons: " + buttons);
            if (pl.cc_mouse_raw != Vector2.zero)
                Debug.Log("mouse: " + pl.cc_mouse_raw);
            if (pl.cc_turn_vec != Vector3.zero)
                Debug.Log("turn: " + pl.cc_turn_vec);
            if (pl.cc_move_vec != Vector3.zero)
                Debug.Log("move: " + pl.cc_move_vec);
            */
            VeryClassic.VeryClassicSetControls(buttons,
                pl.cc_mouse_raw.x, pl.cc_mouse_raw.y,
                pl.cc_turn_vec.x, pl.cc_turn_vec.y, pl.cc_turn_vec.z,
                pl.cc_move_vec.x, pl.cc_move_vec.y, pl.cc_move_vec.z,
                Controls.m_input_count[(int)CCInput.VIEW_MAP],
                Controls.m_input_count[(int)CCInput.REAR_VIEW]);

            if (VeryClassicFrame(0, 0, play) == -1)
                IsDone = true;
            m_main_texture.LoadRawTextureData(m_screen_data);
            m_main_texture.Apply();
            if (sndbuf == null)
                LoadSounds();
        }

        public static void LoadSounds()
        {
            int sndsize;
            var sndc = VeryClassicGameSoundCount(out sndsize);
            if (sndc == 0)
                return;
            Debug.Log("LoadSounds count: " + sndc + " size " + sndsize);
            sndbuf = new byte[sndsize];
            sndofs = new int[sndc];
            sndlen = new int[sndc];
            VeryClassicGameSoundLoad(sndbuf, sndofs, sndlen);
            Debug.Log("LoadSounds done");
        }

        public static AudioClip GetClip(int sndnum)
        {
            ClassicClip clip;
            if (!clips.TryGetValue(sndnum, out clip))
            {
                clip = clips[sndnum] = new ClassicClip(sndnum);
                //clipSources.Add(clip.clip, clip);
            }
            return clip.clip;
        }

    }

    [HarmonyPatch(typeof(GameManager),  "InitializeMissionList")]
    class VeryClassicAdd
    {
        private static void Postfix(List<Mission> ___m_mission_list)
        {
            if (NetworkManager.IsHeadless())
                return;
            try
            {
                /*
                if (VeryClassic.LoadLibrary("VeryClassic.dll") == IntPtr.Zero)
                {
                    Debug.Log("Missing VeryClassic.dll");
                    return;
                }
                */
                Mission mission = new Mission(MissionType.BUILT_IN, VeryClassic.MissionName, ___m_mission_list.Count);
                mission.m_display_name.Populate("VERY CLASSIC MISSION");
                foreach (var name in VeryClassic.LevelNames)
                    mission.AddLevel(name, name, "");
                List<LevelInfo> levels = (List<LevelInfo>)typeof(Mission).GetField("Levels", AccessTools.all).GetValue(mission);
                var num = 0;
                foreach (var level in levels)
                {
                    typeof(LevelInfo).GetField("m_loaded_language", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(level, Loc.CurrentLanguageCode);
                    typeof(LevelInfo).GetField("m_briefing", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(level, new [] { "" });
                    typeof(LevelInfo).GetProperty("DisplayLevelNum", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(level, ++num, null);
                    typeof(LevelInfo).GetProperty("FilePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(level, level.FileName, null);
                }
                ___m_mission_list.Add(mission);
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "BriefingUpdate")]
    class VeryClassicBriefing
    {
        private static bool Prefix(MenuManager.BriefingPageType type)
        {
            if (type != MenuManager.BriefingPageType.Briefing ||
                GameplayManager.Level.Mission.FileName != "VeryClassicMission")
                return true;
            if (MenuManager.m_menu_sub_state == MenuSubState.INIT)
            {
                MenuManager.m_briefing_page_num = 0;
                MenuManager.m_briefing_text = new [] { "VeryClassic" };
                var scene = (GameObject)Resources.Load("Cutscenes/cutscene_black");
                MenuManager.m_cutscene_go = UnityEngine.Object.Instantiate<GameObject>(scene, Vector3.up * 50f, Quaternion.identity);
                //var post = Camera.main.gameObject.GetComponent<UnityEngine.PostProcessing.PostProcessingBehaviour>();
                //if (post)
                //    post.enabled = false;
                UIManager.ShowCinematicBars(false, false);
                UIManager.SetOverlayAntiAlias(false);
                UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.BRIEFING);
                VeryClassic.Init(MenuManager.m_cutscene_go);
                //UIManager.url[0].SetTexture(VeryClassic.m_main_texture, true);
                UIManager.SetTexture(VeryClassic.m_main_texture);
                GameManager.m_audio.PlayMusic("", 0.4f);
                MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
            }
            if (VeryClassic.IsDone)
                VeryClassic.Done();
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawBriefing")]
    class VeryClassicBriefingDraw
    {
        private static bool Prefix(MenuManager.BriefingPageType type, UIElement __instance)
        {
            if (type != MenuManager.BriefingPageType.Briefing ||
                GameplayManager.Level.Mission.FileName != "VeryClassicMission")
                return true;
            VeryClassic.Update();

            var c = Color.white; // HSBColor.ConvertToColor(0f, 0f, 0.75f);

            /*
            Vector2 pos;
            pos.y = 100f;
            pos.x = -620f;
            string text = "hello";
            __instance.DrawStringSmall(text, pos, 0.9f, StringOffset.LEFT, c, 1f, 1240f);
            */

            //UIManager.SetOverlayAntiAlias(false);
            UIManager.PauseMainDrawing();
            UIManager.StartDrawing(UIManager.url[1], true, 750f);
            UIManager.DrawTileFull(new Vector2(0, 0), 400f, 300f, c, 1f); // 360 270
            UIManager.ResumeMainDrawing();
            return false;
        }
    }
}
