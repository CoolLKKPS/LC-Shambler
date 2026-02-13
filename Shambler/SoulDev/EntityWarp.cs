using UnityEngine;
using UnityEngine.AI;

namespace SoulDev
{
    internal class EntityWarp
    {
        public static EntityWarp.entrancePack findNearestEntrance(EnemyAI __instance)
        {
            float bestDistance = 100000000f;
            EntranceTeleport bestTele = null;
            EntranceTeleport[] array = EntityWarp.mapEntrances;
            for (int i = 0; i < array.Length; i++)
            {
                bool flag = __instance.isOutside == array[i].isEntranceToBuilding && Vector3.Distance(__instance.transform.position, array[i].transform.position) < bestDistance;
                if (flag)
                {
                    bestDistance = Vector3.Distance(__instance.transform.position, array[i].transform.position);
                    bestTele = array[i];
                }
            }
            EntityWarp.entrancePack pack = default(EntityWarp.entrancePack);
            bool flag2 = bestTele != null;
            if (flag2)
            {
                NavMeshHit hit;
                bool result = NavMesh.SamplePosition(bestTele.transform.position, out hit, 10f, -1);
                bool flag3 = result;
                if (flag3)
                {
                    pack.navPosition = hit.position;
                }
            }
            pack.tele = bestTele;
            return pack;
        }

        public static void SendEnemyInside(EnemyAI __instance)
        {
            __instance.isOutside = false;
            __instance.allAINodes = GameObject.FindGameObjectsWithTag("AINode");
            EntranceTeleport doorEntered = EntityWarp.findNearestEntrance(__instance).tele;
            bool flag = !doorEntered;
            if (flag)
            {
                Debug.LogError("MOAI EntranceTeleport: Failed to find entrance teleport.");
            }
            Transform entrancePosition = doorEntered.entrancePoint;
            bool flag2 = !entrancePosition;
            if (flag2)
            {
                Debug.LogError("MOAI EntranceTeleport: Failed to find best exit position.");
            }
            NavMeshHit hit;
            bool result = NavMesh.SamplePosition(entrancePosition.transform.position, out hit, 10f, -1);
            bool flag3 = result;
            if (flag3)
            {
                __instance.serverPosition = hit.position;
                __instance.transform.position = hit.position;
                __instance.agent.Warp(__instance.serverPosition);
                __instance.SyncPositionToClients();
            }
            else
            {
                Debug.LogError("MOAI EntranceTeleport: Failed to find exit NavmeshHit position");
            }
        }

        public static Transform findExitPoint(EntranceTeleport referenceDoor)
        {
            return referenceDoor.exitPoint;
        }

        public static void SendEnemyOutside(EnemyAI __instance, bool SpawnOnDoor = true)
        {
            __instance.isOutside = true;
            __instance.allAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
            EntranceTeleport doorEntered = EntityWarp.findNearestEntrance(__instance).tele;
            bool flag = !doorEntered;
            if (flag)
            {
                Debug.LogError("MOAI EntranceTeleport: Failed to find entrance teleport.");
            }
            Transform entrancePosition = doorEntered.entrancePoint;
            bool flag2 = !entrancePosition;
            if (flag2)
            {
                Debug.LogError("MOAI EntranceTeleport: Failed to find best exit position.");
            }
            NavMeshHit hit;
            bool result = NavMesh.SamplePosition(entrancePosition.transform.position, out hit, 10f, -1);
            bool flag3 = result;
            if (flag3)
            {
                __instance.serverPosition = hit.position;
                __instance.transform.position = hit.position;
                __instance.agent.Warp(__instance.serverPosition);
                __instance.SyncPositionToClients();
            }
            else
            {
                Debug.LogError("MOAI EntranceTeleport: Failed to find exit NavmeshHit position");
            }
        }

        public static EntranceTeleport[] mapEntrances;

        public struct entrancePack
        {
            public EntranceTeleport tele;

            public Vector3 navPosition;
        }
    }
}
