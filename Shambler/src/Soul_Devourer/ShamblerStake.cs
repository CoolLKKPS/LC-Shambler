using System;
using System.Threading.Tasks;
using GameNetcodeStuff;
using SoulDev;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Shambler.src.Soul_Devourer
{
	internal class ShamblerStake : NetworkBehaviour
	{
		public void Start()
		{
			PlayerControllerB plylocal = RoundManager.Instance.playersManager.localPlayerController;
			this.envTrigger = base.GetComponent<InteractTrigger>();
			bool isHost = RoundManager.Instance.IsHost;
			if (isHost)
			{
				this.StartSetupClientRpc();
				NavMeshHit Hit;
				NavMesh.SamplePosition(base.transform.position, out Hit, 10f, -1);
				base.transform.position = new Vector3(Hit.position.x, Hit.position.y + ShamblerStake.commonOffset, Hit.position.z);
				this.SetPositionClientRpc(base.transform.position);
			}
		}

		[ClientRpc]
		public void SetPositionClientRpc(Vector3 pos)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(172827685U, clientRpcParams, RpcDelivery.Reliable);
				fastBufferWriter.WriteValueSafe(in pos);
				base.__endSendClientRpc(ref fastBufferWriter, 172827685U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			base.transform.position = pos;
		}

		public PlayerControllerB NearestPlayer()
		{
			RoundManager i = RoundManager.Instance;
			float lowestDist = 999999f;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			PlayerControllerB nearestPlayer = players[0];
			foreach (PlayerControllerB ply in players)
			{
				bool flag = Vector3.Distance(base.transform.position, ply.transform.position) < lowestDist;
				if (flag)
				{
					nearestPlayer = ply;
					lowestDist = Vector3.Distance(base.transform.position, ply.transform.position);
				}
			}
			return nearestPlayer;
		}

		[ClientRpc]
		public void StartSetupClientRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(2061572526U, clientRpcParams, RpcDelivery.Reliable);
				base.__endSendClientRpc(ref fastBufferWriter, 2061572526U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			bool flag = this.victim == null;
			if (flag)
			{
				this.victim = this.NearestPlayer();
			}
			PlayerControllerB plylocal = RoundManager.Instance.playersManager.localPlayerController;
			this.envTrigger = base.GetComponent<InteractTrigger>();
			bool flag2 = this.victim != null && this.victim.NetworkObject.NetworkObjectId == plylocal.NetworkObject.NetworkObjectId;
			if (flag2)
			{
				this.envTrigger.hoverTip = "Attempt to Escape (" + this.freeChance.ToString() + " % chance) Don't let the shambler notice!";
			}
			else
			{
				this.envTrigger.hoverTip = "Free Player (100 % chance) Don't let the shambler notice!";
			}
		}

		public static void MoveByCenter(GameObject obj, Vector3 targetPosition)
		{
			bool flag = obj == null;
			if (!flag)
			{
				Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
				bool flag2 = renderers.Length == 0;
				if (!flag2)
				{
					Bounds combinedBounds = renderers[0].bounds;
					foreach (Renderer r in renderers)
					{
						combinedBounds.Encapsulate(r.bounds);
					}
					Vector3 offset = obj.transform.position - combinedBounds.center;
					obj.transform.position = targetPosition + offset;
				}
			}
		}

		[ClientRpc]
		public void SetVictimClientRpc(ulong playerid)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(4254107206U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				base.__endSendClientRpc(ref fastBufferWriter, 4254107206U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			RoundManager i = RoundManager.Instance;
			PlayerControllerB plylocal = RoundManager.Instance.playersManager.localPlayerController;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag)
				{
					this.victim = ply;
					bool flag2 = this.victim.NetworkObject.NetworkObjectId == plylocal.NetworkObject.NetworkObjectId;
					if (flag2)
					{
						this.envTrigger.hoverTip = "Attempt to Escape (" + this.freeChance.ToString() + " % chance) Don't let the shambler notice!";
					}
					else
					{
						this.envTrigger.hoverTip = "Free Player (100 % chance) Don't let the shambler notice!";
					}
				}
			}
		}

		public void Update()
		{
			this.checkCooldown -= Time.deltaTime;
			bool flag = this.victim;
			if (flag)
			{
				this.victim.fallValue = 0f;
				this.victim.fallValueUncapped = 0f;
			}
			bool flag2 = RoundManager.Instance.IsHost && this.checkCooldown <= 0f && this.victim == null && this.IsFreeing;
			if (flag2)
			{
				this.checkCooldown = 0f;
				this.owner.PlayerQualifies(this.victim);
			}
			bool flag3 = this.victim;
			if (flag3)
			{
				this.victim.fallValue = 0f;
				this.victim.fallValueUncapped = 0f;
			}
			bool flag4 = this.victim;
			if (flag4)
			{
				this.victim.transform.position = this.stabPoint.position;
				bool enabled = this.victim.playerCollider.enabled;
				if (enabled)
				{
				}
			}
			bool isHost = RoundManager.Instance.IsHost;
			if (isHost)
			{
				bool flag5 = this.victim != null && !this.envTrigger.isBeingHeldByPlayer && this.owner.EscapingEmployees.Contains(this.victim.NetworkObject.NetworkObjectId);
				if (flag5)
				{
					this.IsFreeing = false;
					this.owner.StakeUnNotify(this.victim);
				}
				bool flag6 = this.victim != null;
				if (flag6)
				{
					bool flag7 = (this.owner != null && this.owner.capturedPlayer == this.victim) || this.victim.isPlayerDead;
					if (flag7)
					{
						bool isHost2 = RoundManager.Instance.IsHost;
						if (isHost2)
						{
							this.DelayedUnNotifLong(this.victim);
							this.SetColliderClientRpc(this.victim.NetworkObject.NetworkObjectId, true);
							ShamblerEnemy.stuckPlayerIds.Remove(this.victim.NetworkObject.NetworkObjectId);
							this.ResetFallValuesClientRpc(this.victim.NetworkObject.NetworkObjectId);
							this.DetachClientRpc();
							this.PlaySuccessClientRpc();
							try
							{
								this.DisableInteractClientRpc();
							}
							catch (Exception e)
							{
								Debug.Log("Shambler Stake Error: " + e.ToString());
							}
						}
					}
				}
			}
		}

		[ClientRpc]
		public void DetachClientRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(423408505U, clientRpcParams, RpcDelivery.Reliable);
				base.__endSendClientRpc(ref fastBufferWriter, 423408505U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			bool flag = this.victim != null;
			if (flag)
			{
				bool flag2 = ShamblerEnemy.stuckPlayerIds != null;
				if (flag2)
				{
					ShamblerEnemy.stuckPlayerIds.Remove(this.victim.NetworkObject.NetworkObjectId);
				}
			}
			this.victim = null;
		}

		public void AttemptFree(PlayerControllerB caller)
		{
			Debug.Log("stake attempt free:");
			bool flag = !RoundManager.Instance.IsHost;
			if (flag)
			{
				this.AttemptFreeServerRpc(caller.NetworkObject.NetworkObjectId);
			}
			else
			{
				bool isPlayerDead = this.victim.isPlayerDead;
				if (isPlayerDead)
				{
					this.DetachClientRpc();
				}
				this.owner.StakeNotify(this.victim);
				this.IsFreeing = false;
				bool flag2 = UnityEngine.Random.Range(0, 100) < this.freeChance || caller.NetworkObject.NetworkObjectId != this.victim.NetworkObject.NetworkObjectId;
				if (flag2)
				{
					bool flag3 = this.victim;
					if (flag3)
					{
						this.SnapToNavmesh(this.victim);
						this.DelayedUnNotifLong(this.victim);
						ShamblerEnemy.stuckPlayerIds.Remove(this.victim.NetworkObject.NetworkObjectId);
						this.ResetFallValuesClientRpc(this.victim.NetworkObject.NetworkObjectId);
						this.DetachClientRpc();
						this.PlaySuccessClientRpc();
						try
						{
							this.DisableInteractClientRpc();
						}
						catch (Exception e)
						{
							Debug.Log("Shambler Stake Error: " + e.ToString());
						}
					}
				}
				else
				{
					bool flag4 = this.victim;
					if (flag4)
					{
						this.DmgPlayerClientRpc(this.victim.NetworkObject.NetworkObjectId, this.dmgPunishment);
						this.PlayFailEscapeClientRpc();
						this.updateStatsClientRpc(5, this.failBoost);
						this.owner.StakeUnNotify(this.victim);
					}
				}
			}
		}

		public void SnapToNavmesh(PlayerControllerB ply)
		{
			NavMeshHit hit;
			bool sample = NavMesh.SamplePosition(ply.transform.position, out hit, 10f, -1);
			bool flag = sample;
			if (flag)
			{
				this.NavSnapClientRpc(ply.NetworkObject.NetworkObjectId, hit.position);
				Debug.Log("Snapped to position: " + hit.position.ToString());
			}
		}

		[ClientRpc]
		public void NavSnapClientRpc(ulong playerid, Vector3 pos)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(772911773U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				fastBufferWriter.WriteValueSafe(in pos);
				base.__endSendClientRpc(ref fastBufferWriter, 772911773U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			RoundManager i = RoundManager.Instance;
			PlayerControllerB plylocal = RoundManager.Instance.playersManager.localPlayerController;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag)
				{
					ply.transform.position = pos;
				}
			}
		}

		[ClientRpc]
		public void updateStatsClientRpc(int dmgPunishmentChange, int freeChanceChange)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(4142245935U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, dmgPunishmentChange);
				BytePacker.WriteValueBitPacked(fastBufferWriter, freeChanceChange);
				base.__endSendClientRpc(ref fastBufferWriter, 4142245935U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			PlayerControllerB plylocal = RoundManager.Instance.playersManager.localPlayerController;
			this.dmgPunishment += dmgPunishmentChange;
			this.freeChance += freeChanceChange;
			bool flag = this.victim != null && this.victim.NetworkObject.NetworkObjectId == plylocal.NetworkObject.NetworkObjectId;
			if (flag)
			{
				this.envTrigger.hoverTip = "Attempt to Escape (" + this.freeChance.ToString() + " % chance) Don't let the shambler notice!";
			}
			else
			{
				this.envTrigger.hoverTip = "Free Player (100 % chance) Don't let the shambler notice!";
			}
		}

		[ClientRpc]
		public void DmgPlayerClientRpc(ulong playerid, int amount)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(515288598U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				BytePacker.WriteValueBitPacked(fastBufferWriter, amount);
				base.__endSendClientRpc(ref fastBufferWriter, 515288598U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			RoundManager i = RoundManager.Instance;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag)
				{
					ply.DamagePlayer(30, true, true, CauseOfDeath.Unknown, 0, false, default(Vector3));
				}
			}
		}

		[ClientRpc]
		public void SetHoverTipClientRpc(string tip)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(320229780U, clientRpcParams, RpcDelivery.Reliable);
				bool flag = tip != null;
				fastBufferWriter.WriteValueSafe<bool>(in flag, default(FastBufferWriter.ForPrimitives));
				if (flag)
				{
					fastBufferWriter.WriteValueSafe(tip, false);
				}
				base.__endSendClientRpc(ref fastBufferWriter, 320229780U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			base.GetComponent<InteractTrigger>().hoverTip = tip;
		}

		[ClientRpc]
		public void DisableInteractClientRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(3261615666U, clientRpcParams, RpcDelivery.Reliable);
				base.__endSendClientRpc(ref fastBufferWriter, 3261615666U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			base.GetComponent<InteractTrigger>().enabled = false;
		}

		[ClientRpc]
		public void ResetFallValuesClientRpc(ulong playerid)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(1210762366U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				base.__endSendClientRpc(ref fastBufferWriter, 1210762366U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			RoundManager i = RoundManager.Instance;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag)
				{
					ply.fallValue = 0f;
					ply.fallValueUncapped = 0f;
					ply.playerRigidbody.velocity = Vector3.zero;
				}
			}
		}

		[ClientRpc]
		public void SetColliderClientRpc(ulong playerid, bool value)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(3271024941U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				fastBufferWriter.WriteValueSafe<bool>(in value, default(FastBufferWriter.ForPrimitives));
				base.__endSendClientRpc(ref fastBufferWriter, 3271024941U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			RoundManager i = RoundManager.Instance;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag)
				{
				}
			}
		}

		[ServerRpc(RequireOwnership = false)]
		public void AttemptFreeServerRpc(ulong playerid)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
			{
				ServerRpcParams serverRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendServerRpc(3534838218U, serverRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				base.__endSendServerRpc(ref fastBufferWriter, 3534838218U, serverRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsServer && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			Debug.Log("stake attempt free (SERVERRPC):");
			RoundManager i = RoundManager.Instance;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag)
				{
					this.AttemptFree(ply);
				}
			}
		}

		[ClientRpc]
		public void PlaySuccessClientRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(353136177U, clientRpcParams, RpcDelivery.Reliable);
				base.__endSendClientRpc(ref fastBufferWriter, 353136177U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.successSource.Play();
		}

		[ClientRpc]
		public void PlayFailEscapeClientRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(2512017138U, clientRpcParams, RpcDelivery.Reliable);
				base.__endSendClientRpc(ref fastBufferWriter, 2512017138U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.failEscapeSource.Play();
		}

		public async void DelayedUnNotifLong(PlayerControllerB ply)
		{
			await Task.Delay(9000);
			this.owner.StakeUnNotify(ply);
		}

		public void StartInteract()
		{
			bool isHost = RoundManager.Instance.IsHost;
			if (isHost)
			{
				this.IsFreeing = true;
				this.owner.StakeNotify(this.victim);
			}
		}

		public void StopInteract()
		{
			bool isHost = RoundManager.Instance.IsHost;
			if (isHost)
			{
				this.IsFreeing = false;
				this.owner.StakeUnNotify(this.victim);
			}
		}

		public new void OnDestroy()
		{
			bool flag = this.victim;
			if (flag)
			{
				bool isHost = RoundManager.Instance.IsHost;
				if (isHost)
				{
					bool flag2 = this.victim;
					if (flag2)
					{
						this.SetColliderClientRpc(this.victim.NetworkObject.NetworkObjectId, true);
					}
					ShamblerEnemy.stuckPlayerIds.Remove(this.victim.NetworkObject.NetworkObjectId);
				}
				else
				{
					bool flag3 = this.victim;
					if (flag3)
					{
					}
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__registerRpc(172827685U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_172827685), "SetPositionClientRpc");
			base.__registerRpc(2061572526U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_2061572526), "StartSetupClientRpc");
			base.__registerRpc(4254107206U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_4254107206), "SetVictimClientRpc");
			base.__registerRpc(423408505U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_423408505), "DetachClientRpc");
			base.__registerRpc(772911773U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_772911773), "NavSnapClientRpc");
			base.__registerRpc(4142245935U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_4142245935), "updateStatsClientRpc");
			base.__registerRpc(515288598U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_515288598), "DmgPlayerClientRpc");
			base.__registerRpc(320229780U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_320229780), "SetHoverTipClientRpc");
			base.__registerRpc(3261615666U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_3261615666), "DisableInteractClientRpc");
			base.__registerRpc(1210762366U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_1210762366), "ResetFallValuesClientRpc");
			base.__registerRpc(3271024941U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_3271024941), "SetColliderClientRpc");
			base.__registerRpc(3534838218U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_3534838218), "AttemptFreeServerRpc");
			base.__registerRpc(353136177U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_353136177), "PlaySuccessClientRpc");
			base.__registerRpc(2512017138U, new NetworkBehaviour.RpcReceiveHandler(ShamblerStake.__rpc_handler_2512017138), "PlayFailEscapeClientRpc");
			base.__initializeRpcs();
		}

		private static void __rpc_handler_172827685(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			Vector3 vector;
			reader.ReadValueSafe(out vector);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).SetPositionClientRpc(vector);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_2061572526(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).StartSetupClientRpc();
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_4254107206(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).SetVictimClientRpc(num);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_423408505(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).DetachClientRpc();
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_772911773(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			Vector3 vector;
			reader.ReadValueSafe(out vector);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).NavSnapClientRpc(num, vector);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_4142245935(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			int num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			int num2;
			ByteUnpacker.ReadValueBitPacked(reader, out num2);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).updateStatsClientRpc(num, num2);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_515288598(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			int num2;
			ByteUnpacker.ReadValueBitPacked(reader, out num2);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).DmgPlayerClientRpc(num, num2);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_320229780(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			bool flag;
			reader.ReadValueSafe<bool>(out flag, default(FastBufferWriter.ForPrimitives));
			string text = null;
			if (flag)
			{
				reader.ReadValueSafe(out text, false);
			}
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).SetHoverTipClientRpc(text);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_3261615666(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).DisableInteractClientRpc();
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_1210762366(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).ResetFallValuesClientRpc(num);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_3271024941(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			bool flag;
			reader.ReadValueSafe<bool>(out flag, default(FastBufferWriter.ForPrimitives));
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).SetColliderClientRpc(num, flag);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_3534838218(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).AttemptFreeServerRpc(num);
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_353136177(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).PlaySuccessClientRpc();
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_2512017138(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerStake)target).PlayFailEscapeClientRpc();
			((ShamblerStake)target).__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		protected override string __getTypeName()
		{
			return "ShamblerStake";
		}

		public ShamblerEnemy owner;

		public PlayerControllerB victim;

		public Transform stabPoint = null!;

		private InteractTrigger envTrigger;

		public AudioSource failEscapeSource = null!;

		public AudioSource successSource = null!;

		public float damageTimer = 20f;

		public int dmgAmount = 5;

		private int freeChance = 50;

		private int dmgPunishment = 10;

		private int failBoost = 20;

		private bool IsFreeing = false;

		public static float commonOffset = 1.15f;

		private float checkCooldown = 0.25f;
	}
}
