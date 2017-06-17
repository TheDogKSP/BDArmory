﻿using System.Collections;
using System.Collections.Generic;
using BDArmory.Misc;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Radar
{
    public class RadarWarningReceiver : PartModule
    {
        public delegate void RadarPing(Vessel v, Vector3 source, RWRThreatTypes type, float persistTime);

        public static event RadarPing OnRadarPing;

        public delegate void MissileLaunchWarning(Vector3 source, Vector3 direction);

        public static event MissileLaunchWarning OnMissileLaunch;

        public enum RWRThreatTypes
        {
            SAM = 0,
            Fighter = 1,
            AWACS = 2,
            MissileLaunch = 3,
            MissileLock = 4,
            Detection = 5
        }

        string[] iconLabels = new string[] {"S", "F", "A", "M", "M", "D"};


        public MissileFire weaponManager;

        [KSPField(isPersistant = true)] public bool rwrEnabled;

        public static Texture2D rwrDiamondTexture =
            GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "rwrDiamond", false);

        public static Texture2D rwrMissileTexture =
            GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "rwrMissileIcon", false);

        public static AudioClip radarPingSound;
        public static AudioClip missileLockSound;
        public static AudioClip missileLaunchSound;

        //float lastTimePinged = 0;
        const float minPingInterval = 0.12f;
        const float pingPersistTime = 1;

        const int dataCount = 10;

        public float rwrDisplayRange = 8000;

        public TargetSignatureData[] pingsData;
        public Vector3[] pingWorldPositions;
        List<TargetSignatureData> launchWarnings;

        Transform rt;

        Transform referenceTransform
        {
            get
            {
                if (!rt)
                {
                    rt = new GameObject().transform;
                    rt.parent = part.transform;
                    rt.localPosition = Vector3.zero;
                }
                return rt;
            }
        }

        Rect displayRect = new Rect(0, 0, 256, 256);

        GUIStyle rwrIconLabelStyle;

        AudioSource audioSource;
        public static bool WindowRectRWRInitialized;

        public override void OnAwake()
        {
            radarPingSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/rwrPing");
            missileLockSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/rwrMissileLock");
            missileLaunchSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/mLaunchWarning");
        }

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                pingsData = new TargetSignatureData[dataCount];
                pingWorldPositions = new Vector3[dataCount];
                TargetSignatureData.ResetTSDArray(ref pingsData);
                launchWarnings = new List<TargetSignatureData>();

                rwrIconLabelStyle = new GUIStyle();
                rwrIconLabelStyle.alignment = TextAnchor.MiddleCenter;
                rwrIconLabelStyle.normal.textColor = Color.green;
                rwrIconLabelStyle.fontSize = 12;
                rwrIconLabelStyle.border = new RectOffset(0, 0, 0, 0);
                rwrIconLabelStyle.clipping = TextClipping.Overflow;
                rwrIconLabelStyle.wordWrap = false;
                rwrIconLabelStyle.fontStyle = FontStyle.Bold;

                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.minDistance = 500;
                audioSource.maxDistance = 1000;
                audioSource.spatialBlend = 1;
                audioSource.dopplerLevel = 0;
                audioSource.loop = false;

                UpdateVolume();
                BDArmorySettings.OnVolumeChange += UpdateVolume;

                float size = displayRect.height + 20;
                if (!WindowRectRWRInitialized)
                {
                    BDArmorySettings.WindowRectRwr = new Rect(40, Screen.height - size - 20, size, size + 20);
                    WindowRectRWRInitialized = true;
                }

                List<MissileFire>.Enumerator mf = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (mf.MoveNext())
                {
                    if (mf.Current == null) continue;
                    mf.Current.rwr = this;
                    if (!weaponManager)
                    {
                        weaponManager = mf.Current;
                    }
                }
                mf.Dispose();
                if (rwrEnabled) EnableRWR();
            }
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_UI_VOLUME;
            }
        }

        public void EnableRWR()
        {
            OnRadarPing += ReceivePing;
            OnMissileLaunch += ReceiveLaunchWarning;
            rwrEnabled = true;
        }

        public void DisableRWR()
        {
            OnRadarPing -= ReceivePing;
            OnMissileLaunch -= ReceiveLaunchWarning;
            rwrEnabled = false;
        }

        void OnDestroy()
        {
            OnRadarPing -= ReceivePing;
            OnMissileLaunch -= ReceiveLaunchWarning;
            BDArmorySettings.OnVolumeChange -= UpdateVolume;
        }


        IEnumerator PingLifeRoutine(int index, float lifeTime)
        {
            yield return new WaitForSeconds(Mathf.Clamp(lifeTime - 0.04f, minPingInterval, lifeTime));
            pingsData[index] = TargetSignatureData.noTarget;
        }

        IEnumerator LaunchWarningRoutine(TargetSignatureData data)
        {
            launchWarnings.Add(data);
            yield return new WaitForSeconds(2);
            launchWarnings.Remove(data);
        }

        void ReceiveLaunchWarning(Vector3 source, Vector3 direction)
        {
            if(referenceTransform == null) return;
            if (part == null) return;
            if (weaponManager == null) return;

           

            float sqrDist = (part.transform.position - source).sqrMagnitude;
            if (sqrDist < Mathf.Pow(5000, 2) && sqrDist > Mathf.Pow(100, 2) &&
                Vector3.Angle(direction, part.transform.position - source) < 15)
            {
                StartCoroutine(
                    LaunchWarningRoutine(new TargetSignatureData(Vector3.zero,
                        RadarUtils.WorldToRadar(source, referenceTransform, displayRect, rwrDisplayRange), Vector3.zero,
                        true, (float) RWRThreatTypes.MissileLaunch)));
                PlayWarningSound(RWRThreatTypes.MissileLaunch);

                if (weaponManager && weaponManager.guardMode)
                {
                    weaponManager.FireAllCountermeasures(Random.Range(2, 4));
                    weaponManager.incomingThreatPosition = source;
                }
            }
        }

        void ReceivePing(Vessel v, Vector3 source, RWRThreatTypes type, float persistTime)
        {
            if (v == null) return;
            if (referenceTransform == null) return;
            if (weaponManager == null) return;

            if (rwrEnabled && vessel && v == vessel)
            {
                if (type == RWRThreatTypes.MissileLaunch)
                {
                    StartCoroutine(
                        LaunchWarningRoutine(new TargetSignatureData(Vector3.zero,
                            RadarUtils.WorldToRadar(source, referenceTransform, displayRect, rwrDisplayRange),
                            Vector3.zero, true, (float) type)));
                    PlayWarningSound(type);
                    return;
                }
                else if (type == RWRThreatTypes.MissileLock)
                {
                    if (!BDArmorySettings.ALLOW_LEGACY_TARGETING && weaponManager && weaponManager.guardMode)
                    {
                        weaponManager.FireChaff();
                    }
                }

                int openIndex = -1;
                for (int i = 0; i < dataCount; i++)
                {
                    if (pingsData[i].exists &&
                        ((Vector2) pingsData[i].position -
                         RadarUtils.WorldToRadar(source, referenceTransform, displayRect, rwrDisplayRange)).sqrMagnitude <
                        Mathf.Pow(20, 2))
                    {
                        break;
                    }

                    if (!pingsData[i].exists && openIndex == -1)
                    {
                        openIndex = i;
                    }
                }

                if (openIndex >= 0)
                {
                    referenceTransform.rotation = Quaternion.LookRotation(vessel.ReferenceTransform.up,
                        VectorUtils.GetUpDirection(transform.position));

                    pingsData[openIndex] = new TargetSignatureData(Vector3.zero,
                        RadarUtils.WorldToRadar(source, referenceTransform, displayRect, rwrDisplayRange), Vector3.zero,
                        true, (float) type);
                    pingWorldPositions[openIndex] = source;
                    StartCoroutine(PingLifeRoutine(openIndex, persistTime));

                    PlayWarningSound(type);
                }
            }
        }

        void PlayWarningSound(RWRThreatTypes type)
        {
            if (vessel.isActiveVessel)
            {
                switch (type)
                {
                    case RWRThreatTypes.MissileLaunch:
                        audioSource.Stop();
                        audioSource.clip = missileLaunchSound;
                        audioSource.Play();
                        break;
                    case RWRThreatTypes.MissileLock:
                        if (audioSource.clip == missileLaunchSound && audioSource.isPlaying) break;
                        audioSource.Stop();
                        audioSource.clip = (missileLockSound);
                        audioSource.Play();
                        break;
                    default:
                        if (!audioSource.isPlaying)
                        {
                            audioSource.clip = (radarPingSound);
                            audioSource.Play();
                        }
                        break;
                }
            }
        }


        void OnGUI()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && BDArmorySettings.GAME_UI_ENABLED &&
                vessel.isActiveVessel && rwrEnabled)
            {
                BDArmorySettings.WindowRectRwr = GUI.Window(94353, BDArmorySettings.WindowRectRwr, RWRWindow,
                    "Radar Warning Receiver", HighLogic.Skin.window);
                BDGUIUtils.UseMouseEventInRect(BDArmorySettings.WindowRectRwr);
            }
        }

        void RWRWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, BDArmorySettings.WindowRectRwr.width - 18, 30));
            if (GUI.Button(new Rect(BDArmorySettings.WindowRectRwr.width - 28, 2, 26, 26), "X", HighLogic.Skin.button))
            {
                DisableRWR();
            }
            GUI.BeginGroup(new Rect(10, 30, displayRect.width, displayRect.height));
            GUI.DragWindow(displayRect);

            GUI.DrawTexture(displayRect, VesselRadarData.omniBgTexture, ScaleMode.StretchToFill, false);
            float pingSize = 32;

            for (int i = 0; i < dataCount; i++)
            {
                Vector2 pingPosition = (Vector2) pingsData[i].position;
                pingPosition = Vector2.MoveTowards(displayRect.center, pingPosition, displayRect.center.x - (pingSize/2));
                Rect pingRect = new Rect(pingPosition.x - (pingSize/2), pingPosition.y - (pingSize/2), pingSize,
                    pingSize);
                if (pingsData[i].exists)
                {
                    if (pingsData[i].signalStrength == 4)
                    {
                        GUI.DrawTexture(pingRect, rwrMissileTexture, ScaleMode.StretchToFill, true);
                    }
                    else
                    {
                        GUI.DrawTexture(pingRect, rwrDiamondTexture, ScaleMode.StretchToFill, true);
                        GUI.Label(pingRect, iconLabels[Mathf.RoundToInt(pingsData[i].signalStrength)], rwrIconLabelStyle);
                    }
                }
            }

            List<TargetSignatureData>.Enumerator lw = launchWarnings.GetEnumerator();
            while (lw.MoveNext())
            {
                Vector2 pingPosition = (Vector2) lw.Current.position;
                pingPosition = Vector2.MoveTowards(displayRect.center, pingPosition, displayRect.center.x - (pingSize/2));

                Rect pingRect = new Rect(pingPosition.x - (pingSize/2), pingPosition.y - (pingSize/2), pingSize,
                    pingSize);
                GUI.DrawTexture(pingRect, rwrMissileTexture, ScaleMode.StretchToFill, true);
            }
            lw.Dispose();
            GUI.EndGroup();
        }

        public static void PingRWR(Vessel v, Vector3 source, RWRThreatTypes type, float persistTime)
        {
            if (OnRadarPing != null)
            {
                OnRadarPing(v, source, type, persistTime);
            }
        }

        public static void PingRWR(Ray ray, float fov, RWRThreatTypes type, float persistTime)
        {
            List<Vessel>.Enumerator vessel = FlightGlobals.Vessels.GetEnumerator();
            while (vessel.MoveNext())
            {
                if (vessel.Current == null || !vessel.Current.loaded) continue;
                Vector3 dirToVessel = vessel.Current.transform.position - ray.origin;
                if (Vector3.Angle(ray.direction, dirToVessel) < fov/2)
                {
                    PingRWR(vessel.Current, ray.origin, type, persistTime);
                }
             }
            vessel.Dispose();
        }

        public static void WarnMissileLaunch(Vector3 source, Vector3 direction)
        {
            OnMissileLaunch?.Invoke(source, direction);
        }
    }
}