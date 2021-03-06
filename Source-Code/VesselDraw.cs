using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DistantObject
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class VesselDraw : MonoBehaviour
    {
        public static Dictionary<Vessel, List<GameObject>> meshListLookup = new Dictionary<Vessel, List<GameObject>>();
        public static Dictionary<GameObject, ProtoPartSnapshot> referencePart = new Dictionary<GameObject, ProtoPartSnapshot>();
        public static Dictionary<Vessel, bool> vesselIsBuilt = new Dictionary<Vessel, bool>();
        public static List<Vessel> watchList = new List<Vessel>();

        public static Dictionary<string, string> partModelNameLookup = new Dictionary<string, string>();

        public static Vessel workingTarget = null;
        public int n = 0;

        public static Vessel storedTarget = null;
        public static bool renderVessels = false;
        public static float maxDistance = 750000;
        public static int renderMode = 1;
        public static bool ignoreDebris = false;
        public static bool debugMode = false;

        public static void DrawVessel(Vessel shipToDraw)
        {
            if (!vesselIsBuilt[shipToDraw])
            {
                if (debugMode) { print("DistObj: Drawing vessel " + shipToDraw.vesselName); }

                vesselIsBuilt[shipToDraw] = true;

                List<ProtoPartSnapshot> partList = shipToDraw.protoVessel.protoPartSnapshots;
                foreach (ProtoPartSnapshot a in partList)
                {
                    string partName;
                    if (a.refTransformName.Contains(" "))
                        partName = a.partName.Substring(0, a.refTransformName.IndexOf(" "));
                    else
                        partName = a.partName;

                    AvailablePart avPart = PartLoader.getPartInfoByName(partName);

                    if (a.modules.Find(n => n.moduleName == "LaunchClamp") != null)
                    {
                        if (debugMode) { print("Ignoring part " + partName); }
                        continue;
                    }

                    if(!partModelNameLookup.ContainsKey(partName))
                    {
                        partName = partName.Replace('.', '_');
                        if (!partModelNameLookup.ContainsKey(partName))
                        {
                            if (debugMode) { print("DistObj ERROR: Could not find config definition for " + partName); }
                            continue;
                        }
                    }

                    GameObject clone = GameDatabase.Instance.GetModel(partModelNameLookup[partName]);
                    if (clone == null)
                    {
                        if (debugMode) { print("DistObj ERROR: Could not load part model " + partModelNameLookup[partName]); }
                        continue;
                    }
                    GameObject cloneMesh = Mesh.Instantiate(clone) as GameObject;
                    DestroyObject(clone);
                    cloneMesh.transform.SetParent(shipToDraw.transform);
                    cloneMesh.transform.localPosition = a.position;
                    cloneMesh.transform.localRotation = a.rotation;
                    if (Vector3d.Distance(cloneMesh.transform.position, FlightGlobals.ship_position) < Vessel.loadDistance)
                    {
                        print("DistObj ERROR: Tried to draw part " + partName + " within rendering distance of active vessel!");
                        continue;
                    }
                    cloneMesh.SetActive(true);

                    foreach (Collider col in cloneMesh.GetComponentsInChildren<Collider>())
                    {
                        col.enabled = false;
                    }
                    
                    //check if part is a solar panel
                    ProtoPartModuleSnapshot solarPanel = a.modules.Find(n => n.moduleName == "ModuleDeployableSolarPanel");
                    if (solarPanel != null)
                    {
                        if (solarPanel.moduleValues.GetValue("stateString") == "EXTENDED")
                        {
                            //grab the animation name specified in the part cfg
                            string animName = avPart.partPrefab.GetComponent<ModuleDeployableSolarPanel>().animationName;
                            //grab the actual animation istelf
                            AnimationClip animClip = avPart.partPrefab.FindModelAnimators().FirstOrDefault().GetClip(animName);
                            //grab the animation control module on the actual drawn model
                            Animation anim = cloneMesh.GetComponentInChildren<Animation>();
                            //copy the animation over to the new part!
                            anim.AddClip(animClip, animName);
                            anim[animName].enabled = true;
                            anim[animName].normalizedTime =1f;
                        }
                    }

                    //check if part is a light
                    ProtoPartModuleSnapshot light = a.modules.Find(n => n.moduleName == "ModuleLight");
                    if (light != null)
                    {
                        //Oddly enough the light already renders no matter what, so we'll kill the module if it's suppsed to be turned off
                        if (light.moduleValues.GetValue("isOn") == "False")
                        {
                            Destroy(cloneMesh.GetComponentInChildren<Light>());
                        }
                    }

                    //check if part is a landing leg
                    ProtoPartModuleSnapshot landingLeg = a.modules.Find(n => n.moduleName == "ModuleLandingLeg");
                    if (landingLeg != null)
                    {
                        if (landingLeg.moduleValues.GetValue("savedAnimationTime") != "0")
                        {
                            //grab the animation name specified in the part cfg
                            string animName = avPart.partPrefab.GetComponent<ModuleLandingLeg>().animationName;
                            //grab the actual animation istelf
                            AnimationClip animClip = avPart.partPrefab.FindModelAnimators().FirstOrDefault().GetClip(animName);
                            //grab the animation control module on the actual drawn model
                            Animation anim = cloneMesh.GetComponentInChildren<Animation>();
                            //copy the animation over to the new part!
                            anim.AddClip(animClip, animName);
                            anim[animName].enabled = true;
                            anim[animName].normalizedTime = 1f;
                        }
                    }

                    //check if part has a generic animation
                    ProtoPartModuleSnapshot animGeneric = a.modules.Find(n => n.moduleName == "ModuleAnimateGeneric");
                    if (animGeneric != null)
                    {
                        if (animGeneric.moduleValues.GetValue("animTime") != "0")
                        {
                            //grab the animation name specified in the part cfg
                            string animName = avPart.partPrefab.GetComponent<ModuleAnimateGeneric>().animationName;
                            //grab the actual animation istelf
                            AnimationClip animClip = avPart.partPrefab.FindModelAnimators().FirstOrDefault().GetClip(animName);
                            //grab the animation control module on the actual drawn model
                            Animation anim = cloneMesh.GetComponentInChildren<Animation>();
                            //copy the animation over to the new part!
                            anim.AddClip(animClip, animName);
                            anim[animName].enabled = true;
                            anim[animName].normalizedTime = 1f;
                        }
                    }

                    referencePart.Add(cloneMesh, a);
                    meshListLookup[shipToDraw].Add(cloneMesh);
                }
            }
        }

        public static void CheckErase(Vessel shipToErase)
        {
            if (vesselIsBuilt[shipToErase])
            {
                if (debugMode) { print("DistObj: Erasing vessel " + shipToErase.vesselName + " (vessel unloaded)"); }

                foreach (GameObject mesh in meshListLookup[shipToErase])
                {
                    UnityEngine.GameObject.Destroy(mesh);
                }
                vesselIsBuilt[shipToErase] = false;
                meshListLookup[shipToErase].Clear();
                watchList.Remove(shipToErase);
                workingTarget = null;
            }
        }

        public static void VesselCheck(Vessel shipToCheck)
        {
            if (Vector3d.Distance(shipToCheck.GetWorldPos3D(), FlightGlobals.ship_position) < maxDistance && !shipToCheck.loaded)
            {
                if (!vesselIsBuilt.ContainsKey(shipToCheck))
                {
                    meshListLookup.Add(shipToCheck, new List<GameObject>());
                    vesselIsBuilt.Add(shipToCheck, false);
                    watchList.Add(shipToCheck);
                    if (debugMode) { print("DistObj: Adding new definition for " + shipToCheck.vesselName); }
                }
                DrawVessel(shipToCheck);
            }
            else
            {
                if (!vesselIsBuilt.ContainsKey(shipToCheck))
                {
                    meshListLookup.Add(shipToCheck, new List<GameObject>());
                    vesselIsBuilt.Add(shipToCheck, false);
                    watchList.Add(shipToCheck);
                    if (debugMode) { print("DistObj: Adding new definition for " + shipToCheck.vesselName); }
                }
                CheckErase(shipToCheck);
            }
        }

        private void Update()
        {
            if (renderVessels)
            {
                restart:
                foreach (Vessel vessel in watchList)
                {
                    if (!FlightGlobals.fetch.vessels.Contains(vessel))
                    {
                        if (debugMode) { print("DistObj: Erasing vessel " + vessel.vesselName + " (vessel destroyed)"); }

                        if (vesselIsBuilt.ContainsKey(vessel))
                            vesselIsBuilt.Remove(vessel);
                        if (meshListLookup.ContainsKey(vessel))
                            meshListLookup.Remove(vessel);
                        watchList.Remove(vessel);
                        workingTarget = null;

                        goto restart;
                    }
                }

                if (renderMode == 0)
                {
                    var target = FlightGlobals.fetch.VesselTarget;
                    if (target != null)
                    {
                        if (target.GetType().Name == "Vessel")
                        {
                            workingTarget = FlightGlobals.Vessels.Find(index => index.GetName() == target.GetName());
                            VesselCheck(workingTarget);
                        }
                        else if (workingTarget != null)
                            CheckErase(workingTarget);
                    }
                    else if (workingTarget != null)
                        CheckErase(workingTarget);
                }
                else if (renderMode == 1)
                {
                    n += 1;
                    if (n >= FlightGlobals.Vessels.Count)
                        n = 0;
                    if (FlightGlobals.Vessels[n].vesselType != VesselType.Flag && FlightGlobals.Vessels[n].vesselType != VesselType.EVA && (FlightGlobals.Vessels[n].vesselType != VesselType.Debris || !ignoreDebris))
                        VesselCheck(FlightGlobals.Vessels[n]);
                }
            }
        }

        public void Awake()
        {
            //Load settings
            ConfigNode settings = ConfigNode.Load(KSPUtil.ApplicationRootPath + "GameData/DistantObject/Settings.cfg");
            foreach (ConfigNode node in settings.GetNodes("DistantVessel"))
            {
                renderVessels = bool.Parse(node.GetValue("renderVessels"));
                maxDistance = float.Parse(node.GetValue("maxDistance"));
                renderMode = int.Parse(node.GetValue("renderMode"));
                ignoreDebris = bool.Parse(node.GetValue("ignoreDebris"));
                debugMode = bool.Parse(node.GetValue("debugMode"));
            }

            meshListLookup.Clear();
            referencePart.Clear();
            vesselIsBuilt.Clear();
            watchList.Clear();
            partModelNameLookup.Clear();

            foreach (UrlDir.UrlConfig urlConfig in GameDatabase.Instance.GetConfigs("PART"))
            {
                ConfigNode cfgNode = ConfigNode.Load(urlConfig.parent.fullPath);
                foreach(ConfigNode node in cfgNode.nodes)
                {
                    if (node.GetValue("name") == urlConfig.name)
                        cfgNode = node;
                }

                if (cfgNode.HasValue("name"))
                {
                    string url = urlConfig.parent.url.Substring(0, urlConfig.parent.url.LastIndexOf("/"));
                    string model = System.IO.Path.GetFileNameWithoutExtension(cfgNode.GetValue("mesh"));
                    partModelNameLookup.Add(urlConfig.name, url + "/" + model);
                }
                else
                    print("DistObj ERROR: Could not find ConfigNode for part " + urlConfig.name);
            }

            if (renderVessels) { print("Distant Object Enhancement v1.3 -- VesselDraw initialized"); }
            else { print("Distant Object Enhancement v1.3 -- VesselDraw disabled"); }
        }
    }
}
