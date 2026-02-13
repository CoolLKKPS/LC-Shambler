using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using GameNetcodeStuff;
using Shambler;
using Shambler.src.Soul_Devourer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace SoulDev
{
	internal class ShamblerEnemy : EnemyAI
	{
		private void Think(string msg)
		{
			bool flag = !this.debugThoughts;
			if (!flag)
			{
				Debug.Log(string.Format("[Shambler@{0:F1}] {1}", Time.time, msg));
			}
		}

		private void OnDrawGizmosSelected()
		{
			bool flag = !this.debugDrawGoal || this.lastGoal == Vector3.zero;
			if (!flag)
			{
				Gizmos.color = Color.cyan;
				Gizmos.DrawLine(base.transform.position + Vector3.up * 0.5f, this.lastGoal + Vector3.up * 0.5f);
				Gizmos.DrawSphere(this.lastGoal, 0.25f);
			}
		}

		public void LogDebug(string text)
		{
			Plugin.Logger.LogInfo(text);
		}

		public void facePosition(Vector3 pos)
		{
			Vector3 directionToTarget = pos - base.transform.position;
			directionToTarget.y = 0f;
			bool flag = directionToTarget != Vector3.zero;
			if (flag)
			{
				Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
				base.transform.rotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
		}

		public PlayerControllerB getNearestPlayer(bool addFilter = true)
		{
			PlayerControllerB[] players = RoundManager.Instance.playersManager.allPlayerScripts;
			PlayerControllerB bestPlayer = null;
			float bestDistance = 1E+09f;
			foreach (PlayerControllerB player in players)
			{
				bool flag = player && !player.isPlayerDead && player.isPlayerControlled;
				if (flag)
				{
					float d = Vector3.Distance(base.transform.position, player.transform.position);
					bool flag2 = d < bestDistance;
					if (flag2)
					{
						bool flag3 = this.PlayerQualifies(player) || !addFilter;
						if (flag3)
						{
							bestDistance = d;
							bestPlayer = player;
						}
					}
				}
			}
			return bestPlayer;
		}

		public void SetAudioVolumes()
		{
		}

		public override void Start()
		{
			base.Start();
			bool flag = RoundManager.Instance.IsHost && Object.FindObjectsOfType<ShamblerEnemy>().Length > Plugin.maxCount.Value;
			if (flag)
			{
				Object.Destroy(base.gameObject);
				Debug.Log("Shambler: Destroyed self (enemy count too high)");
			}
			this.maxStabDistance = 5f * base.transform.localScale.x;
			this.maxLeapDistance = 20f * base.transform.localScale.x;
			this.captureRange = 5.35f * base.transform.localScale.x;
			this.nestSpot = base.transform.position;
			EntityWarp.mapEntrances = Object.FindObjectsOfType<EntranceTeleport>(false);
			this.mostRecentPlayer = this.getNearestPlayer(true);
			this.animator = base.gameObject.GetComponent<Animator>();
			bool isHost = RoundManager.Instance.IsHost;
			if (isHost)
			{
				this.DoAnimationClientRpc(0);
			}
			this.stamina = 120f;
			this.timeSinceHittingLocalPlayer = 0f;
			this.timeSinceNewRandPos = 0f;
			this.positionRandomness = new Vector3(0f, 0f, 0f);
			this.enemyRandom = new Random(StartOfRound.Instance.randomMapSeed + this.thisEnemyIndex);
			this.isDeadAnimationDone = false;
			this.currentBehaviourStateIndex = 0;
			base.StartSearch(base.transform.position, null);
			this.moaiSoundPlayClientRpc("creatureVoice");
			this.enemyHP = Plugin.health.Value;
			this.WorldMask = this.BuildWorldMask();
			this.Think(string.Format("WorldMask={0} built.", this.WorldMask.value));
			PlayerControllerB np = this.getNearestPlayer(true);
			bool flag2 = np;
			if (flag2)
			{
				Vector3 from = (np.playerEye ? np.playerEye.transform.position : (np.transform.position + Vector3.up * 1.6f));
				Vector3 to = base.transform.position + Vector3.up * 1.2f;
				RaycastHit hit;
				bool blocked = Physics.Linecast(from, to, out hit, this.WorldMask, QueryTriggerInteraction.Ignore);
				this.Think(string.Format("Initial LOS blocked={0}, hit={1}, layer={2}", blocked, hit.collider ? hit.collider.name : "none", hit.collider ? LayerMask.LayerToName(hit.collider.gameObject.layer) : "none"));
			}
			bool flag3 = this.currentBehaviourStateIndex == 3 && this.targetPlayer;
			if (flag3)
			{
				this.facePosition(this.targetPlayer.transform.position);
			}
		}

		private static bool IsPlayerStaked(PlayerControllerB p)
		{
			bool flag = p == null || p.NetworkObject == null;
			return !flag && ShamblerEnemy.stuckPlayerIds.Contains(p.NetworkObject.NetworkObjectId);
		}

		public override void Update()
		{
			base.Update();
			this.sizeCheckCooldown -= Time.deltaTime;
			bool flag = this.sizeCheckCooldown < 0f;
			if (flag)
			{
				bool flag2 = base.transform.localScale.x > 1f || base.transform.localScale.y > 1f || base.transform.localScale.z > 1f;
				if (flag2)
				{
					base.transform.localScale = new Vector3(1f, 1f, 1f);
					this.maxStabDistance = 5f * base.transform.localScale.x;
					this.maxLeapDistance = 20f * base.transform.localScale.x;
					this.captureRange = 5.35f * base.transform.localScale.x;
				}
				this.sizeCheckCooldown = 2f;
			}
			bool flag3 = !this.isEnemyDead && this.enemyHP <= 0 && !this.markDead;
			if (flag3)
			{
				this.animator.speed = 1f;
				base.KillEnemyOnOwnerClient(false);
				this.stopAllSound();
				bool flag4 = !this.animator.GetCurrentAnimatorStateInfo(0).IsName("Death") && !this.animator.GetCurrentAnimatorStateInfo(0).IsName("Exit");
				if (flag4)
				{
					this.animator.Play("Death");
				}
				this.isEnemyDead = true;
				this.enemyHP = 0;
				this.moaiSoundPlayClientRpc("creatureDeath");
				this.deadEventClientRpc();
				this.markDead = true;
				base.GetComponent<BoxCollider>().enabled = false;
			}
			bool isEnemyDead = this.isEnemyDead;
			if (isEnemyDead)
			{
				bool flag5 = this.stabbedPlayer;
				if (flag5)
				{
					this.stabbedPlayer = null;
				}
				bool flag6 = this.capturedPlayer;
				if (flag6)
				{
					this.capturedPlayer = null;
				}
				bool flag7 = !this.animator.GetCurrentAnimatorStateInfo(0).IsName("Death") && !this.animator.GetCurrentAnimatorStateInfo(0).IsName("StayDead");
				if (flag7)
				{
					this.animator.Play("StayDead");
				}
			}
			else
			{
				bool flag8 = this.stabbedPlayer;
				if (flag8)
				{
					bool flag9 = this.stabbedPlayer.playerCollider.enabled && !this.stabbedPlayer.isPlayerDead;
					if (flag9)
					{
					}
					this.stabbedPlayer.fallValue = 0f;
					this.stabbedPlayer.fallValueUncapped = 0f;
					bool isPlayerDead = this.stabbedPlayer.isPlayerDead;
					if (isPlayerDead)
					{
					}
				}
				bool flag10 = this.capturedPlayer;
				if (flag10)
				{
					bool flag11 = this.capturedPlayer.playerCollider.enabled && !this.capturedPlayer.isPlayerDead;
					if (flag11)
					{
					}
					this.capturedPlayer.fallValue = 0f;
					this.capturedPlayer.fallValueUncapped = 0f;
					bool isPlayerDead2 = this.capturedPlayer.isPlayerDead;
					if (isPlayerDead2)
					{
					}
				}
				bool isEnemyDead2 = this.isEnemyDead;
				if (isEnemyDead2)
				{
					bool flag12 = !this.isDeadAnimationDone;
					if (flag12)
					{
						this.animator.speed = 1f;
						this.isDeadAnimationDone = true;
						this.stopAllSound();
						this.creatureVoice.PlayOneShot(this.dieSFX);
					}
				}
				else
				{
					AnimatorStateInfo state = this.animator.GetCurrentAnimatorStateInfo(0);
					bool flag13 = state.IsName("Walk");
					if (flag13)
					{
						float loopT = state.normalizedTime - Mathf.Floor(state.normalizedTime);
						bool flag14 = loopT < 0.1f;
						if (flag14)
						{
							this.stepSoundCycle1 = false;
							this.stepSoundCycle2 = false;
						}
						bool flag15 = loopT > 0.15f && !this.stepSoundCycle1;
						if (flag15)
						{
							this.moaiSoundPlayClientRpc("step");
							this.stepSoundCycle1 = true;
						}
						bool flag16 = loopT > 0.5f && !this.stepSoundCycle2;
						if (flag16)
						{
							this.moaiSoundPlayClientRpc("step");
							this.stepSoundCycle2 = true;
						}
					}
					else
					{
						this.stepSoundCycle1 = (this.stepSoundCycle2 = false);
					}
					bool flag17 = this.alertLevel > this.sneakyAlertLevel && RoundManager.Instance.IsHost;
					if (flag17)
					{
						bool flag18 = this.angerSoundTimer <= 0f;
						if (flag18)
						{
							this.angerSoundTimer = 5f;
							this.moaiSoundPlayClientRpc("creatureAnger");
						}
					}
					bool flag19 = this.angerSoundTimer >= 0f;
					if (flag19)
					{
						this.angerSoundTimer -= Time.deltaTime;
					}
					bool flag20 = this.targetPlayer != null && this.targetPlayer.isPlayerDead;
					if (flag20)
					{
						this.targetPlayer = null;
					}
					this.movingTowardsTargetPlayer = this.targetPlayer != null && !this.usingCustomGoal;
					this.timeSinceHittingLocalPlayer += Time.deltaTime;
					this.timeSinceNewRandPos += Time.deltaTime;
					bool flag21 = this.targetPlayer != null && base.PlayerIsTargetable(this.targetPlayer, false, false);
					if (flag21)
					{
						Transform transform = this.turnCompass;
						if (transform != null)
						{
							transform.LookAt(this.targetPlayer.gameplayCamera.transform.position);
						}
					}
					bool flag22 = this.stunNormalizedTimer > 0f && RoundManager.Instance.IsHost;
					if (flag22)
					{
					}
				}
			}
		}

		private void LateUpdate()
		{
			bool isEnemyDead = this.isEnemyDead;
			if (!isEnemyDead)
			{
				bool flag = this.capturedPlayer;
				if (flag)
				{
					this.capturedPlayer.transform.position = this.capturePoint.position;
				}
				bool flag2 = this.stabbedPlayer;
				if (flag2)
				{
					this.stabbedPlayer.transform.position = this.stabPoint.position;
				}
				this.timeTillStab -= Time.deltaTime;
			}
		}

		public override void DoAIInterval()
		{
			base.DoAIInterval();
			bool isEnemyDead = this.isEnemyDead;
			if (!isEnemyDead)
			{
				bool flag = this.provokePoints > 0;
				if (flag)
				{
					this.provokePoints--;
				}
				bool flag2 = this.spottedCooldown > 0f;
				if (flag2)
				{
					this.spottedCooldown = Mathf.Max(0f, this.spottedCooldown - 1f);
				}
				bool flag3 = this.entranceDelay > 0;
				if (flag3)
				{
					this.entranceDelay--;
				}
				bool flag4 = this.sourcecycle > 0;
				if (flag4)
				{
					this.sourcecycle--;
				}
				else
				{
					this.sourcecycle = 75;
					this.unreachableEnemies.Clear();
				}
				bool flag5 = this.stamina <= 0f;
				if (flag5)
				{
					this.recovering = true;
				}
				else
				{
					bool flag6 = this.stamina > 60f;
					if (flag6)
					{
						this.recovering = false;
					}
				}
				bool flag7 = this.sourcecycle % 5 == 0;
				if (flag7)
				{
					EntityWarp.entrancePack ePack = EntityWarp.findNearestEntrance(this);
					this.nearestEntrance = ePack.tele;
					this.nearestEntranceNavPosition = ePack.navPosition;
					bool flag8 = this.stamina < 120f;
					if (flag8)
					{
						this.stamina += 8f;
					}
					this.mostRecentPlayer = this.getNearestPlayer(true);
				}
				bool flag9 = this.targetPlayer != null;
				if (flag9)
				{
					this.mostRecentPlayer = this.targetPlayer;
				}
				this.AIInterval();
				bool flag10 = this.alertLevel > 0f;
				if (flag10)
				{
					this.alertLevel = Mathf.Max(0f, this.alertLevel - this.alertDecay);
				}
				this.alertLevel = Mathf.Min(100f, this.alertLevel);
			}
		}

		public void AIInterval()
		{
			bool flag = this.capturedPlayer && !this.stabbedCapturedPlayer && this.timeTillStab < 0f && this.currentBehaviourStateIndex != 5;
			if (flag)
			{
				base.StopSearch(this.currentSearch, true);
				base.SwitchToBehaviourClientRpc(5);
				this.isStabbing = false;
				this.doneStab = false;
			}
			switch (this.currentBehaviourStateIndex)
			{
			case 0:
				this.usingCustomGoal = false;
				this.baseSearchingForPlayer(62f);
				break;
			case 1:
				this.usingCustomGoal = false;
				this.baseHeadingToEntrance();
				break;
			case 2:
				this.baseCrouching();
				break;
			case 3:
				this.baseLeaping();
				break;
			case 4:
				this.baseClosingDistance();
				break;
			case 5:
				this.baseStabbingCapturedPlayer();
				break;
			case 6:
				this.baseHeadingToNest();
				break;
			case 7:
				this.basePlantingStake();
				break;
			case 8:
				this.baseSneakyStab();
				break;
			default:
				this.LogDebug("This Behavior State doesn't exist!");
				break;
			}
		}

		public void baseSneakyStab()
		{
			this.agent.speed = 0f;
			this.stabTimeout -= 0.2f;
			this.setAnimationSpeedClientRpc(1f);
			this.agent.updateRotation = false;
			bool flag = this.getNearestPlayer(true);
			if (flag)
			{
				this.facePosition(this.getNearestPlayer(true).transform.position);
			}
			bool isEnemyDead = this.isEnemyDead;
			if (isEnemyDead)
			{
				this.agent.updateRotation = true;
				this.isStabbing = false;
				this.doneStab = false;
				base.SwitchToBehaviourClientRpc(0);
				base.StartSearch(base.transform.position, null);
				this.Think("Switched to searching for player");
			}
			bool flag2 = !this.isStabbing;
			if (flag2)
			{
				base.StartCoroutine(this.DoSneakyStab());
				this.stabTimeout = 6f;
				this.isStabbing = true;
			}
			else
			{
				bool flag3 = this.doneStab;
				if (flag3)
				{
					this.agent.updateRotation = true;
					this.isStabbing = false;
					this.doneStab = false;
					bool flag4 = !this.stabbedPlayer;
					if (flag4)
					{
						this.stabbedPlayer = this.getNearestPlayer(true);
						bool flag5 = this.stabbedPlayer != null;
						if (flag5)
						{
							this.SetStabbedPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
						}
					}
					base.SwitchToBehaviourClientRpc(0);
					base.StartSearch(base.transform.position, null);
					this.Think("Switched to searching for player");
				}
				else
				{
					bool flag6 = this.stabTimeout < 0f;
					if (flag6)
					{
						this.agent.updateRotation = true;
						this.isStabbing = false;
						this.doneStab = false;
						bool flag7 = !this.stabbedPlayer;
						if (flag7)
						{
							this.stabbedPlayer = this.getNearestPlayer(true);
							bool flag8 = this.stabbedPlayer != null;
							if (flag8)
							{
								this.SetStabbedPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
							}
						}
						base.SwitchToBehaviourClientRpc(0);
						base.StartSearch(base.transform.position, null);
						this.Think("Switched to searching for player");
					}
				}
			}
		}

		[ClientRpc]
		public void SetCapturedPlayerClientRpc(ulong playerid, bool reset = false)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(2925436343U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				fastBufferWriter.WriteValueSafe<bool>(in reset, default(FastBufferWriter.ForPrimitives));
				base.__endSendClientRpc(ref fastBufferWriter, 2925436343U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			bool flag = reset;
			if (flag)
			{
				this.capturedPlayer = null;
			}
			RoundManager i = RoundManager.Instance;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag2 = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag2)
				{
					this.capturedPlayer = ply;
				}
			}
		}

		[ClientRpc]
		public void SetStabbedPlayerClientRpc(ulong playerid, bool reset = false)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(3110006365U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				fastBufferWriter.WriteValueSafe<bool>(in reset, default(FastBufferWriter.ForPrimitives));
				base.__endSendClientRpc(ref fastBufferWriter, 3110006365U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			bool flag = reset;
			if (flag)
			{
				this.stabbedPlayer = null;
			}
			RoundManager i = RoundManager.Instance;
			PlayerControllerB[] players = i.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag2 = ply.NetworkObject.NetworkObjectId == playerid;
				if (flag2)
				{
					this.stabbedPlayer = ply;
				}
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
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(3897930302U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				BytePacker.WriteValueBitPacked(fastBufferWriter, amount);
				base.__endSendClientRpc(ref fastBufferWriter, 3897930302U, clientRpcParams, RpcDelivery.Reliable);
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

		public IEnumerator DoSneakyStab()
		{
			bool flag = this.getNearestPlayer(true);
			if (flag)
			{
				this.Think("DoSneakyStab");
				this.DoAnimationClientRpc(7);
				this.animPlayClientRpc("SneakyStab");
				this.moaiSoundPlayClientRpc("creatureSneakyStab");
				yield return new WaitForSeconds(0.4f);
				this.stabbedPlayer = this.getNearestPlayer(true);
				bool flag2 = this.stabbedPlayer != null;
				if (flag2)
				{
					this.SetStabbedPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
				}
				this.capturedPlayer = null;
				this.SetCapturedPlayerClientRpc(0UL, true);
				this.stabbedCapturedPlayer = true;
				this.DmgPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, 30);
				yield return new WaitForSeconds(0.65f);
				this.doneStab = true;
				this.Think("Stab Done");
			}
			else
			{
				this.agent.updateRotation = true;
				this.isStabbing = false;
				this.doneStab = false;
				bool flag3 = !this.stabbedPlayer;
				if (flag3)
				{
					this.stabbedPlayer = this.getNearestPlayer(true);
					bool flag4 = this.stabbedPlayer != null;
					if (flag4)
					{
						this.SetStabbedPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
					}
				}
				base.SwitchToBehaviourClientRpc(0);
				base.StartSearch(base.transform.position, null);
				this.Think("Switched to searching for player");
			}
			yield break;
		}

		public void baseStabbingCapturedPlayer()
		{
			this.agent.speed = 0f;
			this.setAnimationSpeedClientRpc(1f);
			bool isEnemyDead = this.isEnemyDead;
			if (isEnemyDead)
			{
				this.agent.updateRotation = true;
				this.isStabbing = false;
				this.doneStab = false;
				base.SwitchToBehaviourClientRpc(0);
				base.StartSearch(base.transform.position, null);
				this.Think("Switched to searching for player");
			}
			bool flag = !this.isStabbing;
			if (flag)
			{
				base.StartCoroutine(this.DoStab());
				this.isStabbing = true;
			}
			else
			{
				bool flag2 = this.doneStab;
				if (flag2)
				{
					this.agent.updateRotation = true;
					this.isStabbing = false;
					this.doneStab = false;
					this.capturedPlayer = null;
					this.SetCapturedPlayerClientRpc(0UL, true);
					bool flag3 = !this.stabbedPlayer;
					if (flag3)
					{
						this.stabbedPlayer = this.getNearestPlayer(true);
						bool flag4 = this.stabbedPlayer != null;
						if (flag4)
						{
							this.SetStabbedPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
						}
					}
					base.SwitchToBehaviourClientRpc(0);
					this.DoAnimationClientRpc(0);
					this.animPlayClientRpc("Idle");
					base.StartSearch(base.transform.position, null);
					this.Think("Switched to searching for player");
				}
			}
		}

		public IEnumerator DoStab()
		{
			this.Think("DoStab");
			this.DoAnimationClientRpc(5);
			this.animPlayClientRpc("StabVictimHeld");
			this.moaiSoundPlayClientRpc("creatureStab");
			bool flag = this.capturedPlayer;
			if (flag)
			{
				this.SetColliderClientRpc(this.capturedPlayer.NetworkObject.NetworkObjectId, false);
			}
			yield return new WaitForSeconds(1.1f);
			this.stabbedPlayer = this.capturedPlayer;
			bool flag2 = this.stabbedPlayer != null;
			if (flag2)
			{
				this.SetStabbedPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
			}
			this.capturedPlayer = null;
			this.SetCapturedPlayerClientRpc(0UL, false);
			this.stabbedCapturedPlayer = true;
			this.DmgPlayerClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, 30);
			yield return new WaitForSeconds(1.3f);
			bool flag3 = this.stabbedPlayer;
			if (flag3)
			{
				this.SetColliderClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
			}
			this.doneStab = true;
			this.Think("Stab Done");
			yield break;
		}

		public void baseLeaping()
		{
			this.agent.updateRotation = false;
			this.agent.speed = 0f;
			this.setAnimationSpeedClientRpc(1f);
			this.targetPlayer = this.getNearestPlayer(true);
			this.leapTimer -= 0.2f;
			bool flag = this.targetPlayer == null;
			if (flag)
			{
				this.agent.updateRotation = true;
				base.SwitchToBehaviourClientRpc(0);
				base.StartSearch(base.transform.position, null);
				this.Think("Switched to searching for player");
			}
			this.facePosition(this.targetPlayer.transform.position);
			bool flag2 = !this.isLeaping && Vector3.Distance(base.transform.position, this.targetPlayer.transform.position) <= this.maxLeapDistance;
			if (flag2)
			{
				this.DoGroundHopClientRpc(this.targetPlayer.transform.position, base.transform.position);
				this.isLeaping = true;
			}
			else
			{
				bool flag3 = this.doneLeap;
				if (flag3)
				{
					this.agent.updateRotation = true;
					base.SwitchToBehaviourClientRpc(0);
					base.StartSearch(base.transform.position, null);
					this.Think("Switched to searching for player");
				}
				else
				{
					bool flag4 = this.leapTimer < 0f;
					if (flag4)
					{
						this.agent.updateRotation = true;
						base.SwitchToBehaviourClientRpc(0);
						base.StartSearch(base.transform.position, null);
						this.Think("Switched to searching for player");
					}
				}
			}
		}

		public IEnumerator DoGroundHop(Vector3 targetPos, Vector3 shamblerStartPos)
		{
			this.Think("DoGroundHop");
			bool isHost = RoundManager.Instance.IsHost;
			if (isHost)
			{
				this.animPlayClientRpc("Leap-Land");
				this.moaiSoundPlayClientRpc("creatureLeapLand");
			}
			base.transform.position = shamblerStartPos;
			float duration = this.leapAnimationLength + Vector3.Distance(base.transform.position, targetPos) / this.maxLeapDistance * 0.5f;
			float elapsed = 0f;
			float arcPeak = this.leapPeakHeight;
			while (elapsed < duration)
			{
				float t = elapsed / duration;
				float height = Mathf.Sin(t * 3.1415927f) * arcPeak;
				base.transform.position = Vector3.Lerp(shamblerStartPos, targetPos, t) + Vector3.up * height;
				elapsed += Time.deltaTime;
				yield return null;
			}
			base.transform.position = targetPos;
			this.Think("Landing...");
			bool isHost2 = RoundManager.Instance.IsHost;
			if (isHost2)
			{
				this.animPlayClientRpc("Land-Capture");
				this.AttemptCapturePlayer();
				this.DoAnimationClientRpc(4);
				yield return new WaitForSeconds(1.2f);
				this.doneLeap = true;
			}
			this.Think("Leap Done");
			yield break;
		}

		[ClientRpc]
		public void DoGroundHopClientRpc(Vector3 targetPos, Vector3 shamblerStartPos)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(2556090093U, clientRpcParams, RpcDelivery.Reliable);
				fastBufferWriter.WriteValueSafe(in targetPos);
				fastBufferWriter.WriteValueSafe(in shamblerStartPos);
				base.__endSendClientRpc(ref fastBufferWriter, 2556090093U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			base.StartCoroutine(this.DoGroundHop(targetPos, shamblerStartPos));
		}

		public void ClaimHuman()
		{
		}

		public void StakeNotify(PlayerControllerB player)
		{
			bool flag = player != null;
			if (flag)
			{
				this.EscapingEmployees.Add(player.NetworkObject.NetworkObjectId);
			}
		}

		public void StakeUnNotify(PlayerControllerB player)
		{
			bool flag = player != null;
			if (flag)
			{
				this.EscapingEmployees.Remove(player.NetworkObject.NetworkObjectId);
			}
			else
			{
				this.EscapingEmployees.Clear();
			}
		}

		public void AttemptCapturePlayer()
		{
			this.Think("Shambler: Attempt Capture Player");
			PlayerControllerB[] players = RoundManager.Instance.playersManager.allPlayerScripts;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = Vector3.Distance(ply.transform.position, base.transform.position) < this.captureRange;
				if (flag)
				{
					bool flag2 = ply != this.capturedPlayer;
					if (flag2)
					{
						this.Think("Shambler: I GOTCHA");
						this.capturedPlayer = ply;
						this.stabbedCapturedPlayer = false;
						this.attachPlayerClientRpc(this.capturedPlayer.NetworkObject.NetworkObjectId, false, 50);
						this.timeTillStab = (float)(2.0 + 30.0 * this.enemyRandom.NextDouble() * this.enemyRandom.NextDouble() * this.enemyRandom.NextDouble());
						this.DmgPlayerClientRpc(ply.NetworkObject.NetworkObjectId, 20);
						return;
					}
					this.DmgPlayerClientRpc(ply.NetworkObject.NetworkObjectId, 70);
				}
			}
			this.Think("Shambler: OHHHH I MISSED!");
		}

		public void baseCrouching()
		{
			this.agent.speed = 0f;
			this.setAnimationSpeedClientRpc(1f);
			this.DoAnimationClientRpc(2);
			this.animPlayClientRpc("Crouching");
			Debug.Log("Crouching... timer: " + this.crouchTimer.ToString());
			this.crouchTimeout -= 0.2f;
			PlayerControllerB ply = ((this.targetPlayer != null && base.PlayerIsTargetable(this.targetPlayer, false, false)) ? this.targetPlayer : this.getNearestPlayer(true));
			bool flag = Vector3.Distance(base.transform.position, ply.transform.position) > this.maxLeapDistance;
			if (flag)
			{
				base.StartSearch(base.transform.position, null);
				base.SwitchToBehaviourClientRpc(0);
			}
			else
			{
				this.crouchTimer -= 0.2f;
				bool flag2 = this.crouchTimer <= 0f;
				if (flag2)
				{
					this.Think("Switched to leap mode");
					this.doneLeap = false;
					this.isLeaping = false;
					this.leapTimer = 9f;
					base.SwitchToBehaviourClientRpc(3);
				}
				else
				{
					bool flag3 = this.crouchTimeout <= 0f;
					if (flag3)
					{
						base.StartSearch(base.transform.position, null);
						base.SwitchToBehaviourClientRpc(0);
					}
				}
			}
		}

		private LayerMask BuildWorldMask()
		{
			string[] include = new string[] { "Default", "Room", "Colliders", "MiscLevelGeometry", "Terrain", "Railing", "DecalStickableSurface" };
			string[] exclude = new string[] { "Player", "Enemy", "Enemies", "Players", "Ragdoll", "Trigger", "Ignore Raycast", "UI" };
			int mask = 0;
			foreach (string i in include)
			{
				int li = LayerMask.NameToLayer(i);
				bool flag = li >= 0;
				if (flag)
				{
					mask |= 1 << li;
				}
				else
				{
					Debug.LogWarning("[WorldMask] Include layer \"" + i + "\" not found.");
				}
			}
			foreach (string j in exclude)
			{
				int li2 = LayerMask.NameToLayer(j);
				bool flag2 = li2 >= 0;
				if (flag2)
				{
					mask &= ~(1 << li2);
				}
			}
			mask &= ~(1 << base.gameObject.layer);
			bool flag3 = mask == 0;
			if (flag3)
			{
				Debug.LogWarning("[WorldMask] Mask resolved to 0; falling back to Physics.DefaultRaycastLayers.");
				mask = -5 & ~(1 << base.gameObject.layer);
			}
			Debug.Log(string.Format("[WorldMask] Built mask={0} (self layer excluded: {1})", mask, LayerMask.LayerToName(base.gameObject.layer)));
			return mask;
		}

		private float CoverScoreAllPlayers(Vector3 pos)
		{
			PlayerControllerB[] arr = RoundManager.Instance.playersManager.allPlayerScripts;
			int viewers = 0;
			float maxGaze = 0f;
			foreach (PlayerControllerB p in arr)
			{
				bool flag = p == null || !p.isPlayerControlled || p.isPlayerDead;
				if (!flag)
				{
					bool occluded = this.OccludedFromPlayer_Multi(pos, p);
					bool flag2 = !occluded;
					if (flag2)
					{
						viewers++;
						Vector3 toPos = (pos - p.transform.position).normalized;
						float align = Mathf.Max(0f, Vector3.Dot(p.transform.forward, toPos));
						bool flag3 = align > maxGaze;
						if (flag3)
						{
							maxGaze = align;
						}
					}
				}
			}
			return (viewers == 0) ? 2f : (-0.9f * (float)viewers - 0.8f * maxGaze);
		}

		private bool TryFindCoverAgainstPlayer(PlayerControllerB ply, Vector3 around, float searchRadius, float backoff, out Vector3 coverPos)
		{
			coverPos = Vector3.zero;
			bool flag = ply == null;
			bool flag2;
			if (flag)
			{
				flag2 = false;
			}
			else
			{
				Vector3 eye = ((ply.playerEye != null) ? ply.playerEye.transform.position : (ply.transform.position + Vector3.up * 1.6f));
				Vector3 dir = around - eye;
				float maxDist = Mathf.Max(searchRadius, dir.magnitude + searchRadius);
				RaycastHit hit;
				bool flag3 = Physics.Raycast(eye, dir.normalized, out hit, maxDist, this.WorldMask, QueryTriggerInteraction.Ignore);
				if (flag3)
				{
					Vector3 candidate = hit.point + hit.normal * backoff;
					Vector3 navPos;
					bool flag4 = this.TrySampleNavmesh(candidate, 2.5f, out navPos, -1) && this.OccludedFromPlayer_Multi(navPos, ply);
					if (flag4)
					{
						coverPos = navPos;
						return true;
					}
				}
				flag2 = false;
			}
			return flag2;
		}

		private bool TryFindGroupCover(Vector3 center, float minR, float maxR, int rings, int samplesPerRing, out Vector3 best)
		{
			best = Vector3.zero;
			float bestScore = float.NegativeInfinity;
			for (int r = 0; r < rings; r++)
			{
				float t = ((rings == 1) ? 1f : ((float)r / (float)(rings - 1)));
				float radius = Mathf.Lerp(minR, maxR, t);
				for (int s = 0; s < samplesPerRing; s++)
				{
					float ang = (float)s / (float)samplesPerRing * 3.1415927f * 2f;
					Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
					Vector3 raw = center + dir * radius;
					Vector3 navPos;
					bool flag = !this.TrySampleNavmesh(raw, 2.5f, out navPos, -1);
					if (!flag)
					{
						float cover = this.CoverScoreAllPlayers(navPos);
						bool flag2 = cover <= 0.1f;
						if (!flag2)
						{
							float ideal = 9.5f;
							float dist = Vector3.Distance(navPos, center);
							float distScore = 1f - Mathf.Clamp01(Mathf.Abs(dist - ideal) / ideal);
							float myDist = Vector3.Distance(base.transform.position, navPos);
							float travelScore = 1f - Mathf.Clamp01(myDist / 25f);
							float score = 1.4f * cover + 0.8f * distScore + 0.3f * travelScore;
							bool flag3 = score > bestScore;
							if (flag3)
							{
								bestScore = score;
								best = navPos;
							}
						}
					}
				}
			}
			return bestScore > float.NegativeInfinity;
		}

		private bool TryGetMostDangerousViewer(out PlayerControllerB danger, out float align)
		{
			danger = null;
			align = 0f;
			foreach (PlayerControllerB p in RoundManager.Instance.playersManager.allPlayerScripts)
			{
				bool flag = p == null || !p.isPlayerControlled || p.isPlayerDead;
				if (!flag)
				{
					bool flag2 = !this.OccludedFromPlayer_Multi(base.transform.position, p);
					if (flag2)
					{
						Vector3 toMe = (base.transform.position - p.transform.position).normalized;
						float a = Mathf.Max(0f, Vector3.Dot(p.transform.forward, toMe));
						bool flag3 = a > align;
						if (flag3)
						{
							align = a;
							danger = p;
						}
					}
				}
			}
			return danger != null;
		}

		private bool ReachedGoal(Vector3 goal)
		{
			bool flag = goal == Vector3.zero;
			return !flag && Vector3.Distance(base.transform.position, goal) <= this.goalArrivalTol;
		}

		private bool PathBadOrPartial()
		{
			return this.agent.pathStatus == NavMeshPathStatus.PathPartial || this.agent.pathStatus == NavMeshPathStatus.PathInvalid;
		}

		private bool CooldownsAllowRepath()
		{
			bool flag = Time.time - this.stickyPickedAt < this.goalMinLockSeconds;
			bool flag2;
			if (flag)
			{
				flag2 = false;
			}
			else
			{
				bool flag3 = Time.time - this.stickyPickedAt < this.goalRepathCooldown;
				flag2 = !flag3;
			}
			return flag2;
		}

		private void ApplyStickyDestination(Vector3 goal)
		{
			bool flag = goal == Vector3.zero;
			if (!flag)
			{
				bool flag2 = this.stickyGoal != goal;
				if (flag2)
				{
					this.agent.SetDestination(goal);
				}
				this.stickyGoal = goal;
				this.usingCustomGoal = true;
				this.movingTowardsTargetPlayer = false;
			}
		}

		public void DistanceBasedPace(PlayerControllerB ply)
		{
			bool isSprinting = ply.isSprinting;
			if (isSprinting)
			{
				this.agent.speed = 8.2f * Plugin.moaiGlobalSpeed.Value * Math.Max(1f, 16f / Vector3.Distance(base.transform.position, ply.transform.position) * 0.5f + 1f);
			}
			else
			{
				this.agent.speed = 6f * Plugin.moaiGlobalSpeed.Value * Math.Max(1f, 16f / Vector3.Distance(base.transform.position, ply.transform.position) * 0.5f + 1f);
			}
		}

		public Vector3 chooseTravelGoal(out float outScore)
		{
			outScore = float.NegativeInfinity;
			PlayerControllerB ply = ((this.targetPlayer != null && base.PlayerIsTargetable(this.targetPlayer, false, false)) ? this.targetPlayer : this.getNearestPlayer(true));
			bool flag = ply == null;
			Vector3 vector;
			if (flag)
			{
				this.Think("chooseTravelGoal: no valid player — staying put.");
				vector = base.transform.position;
			}
			else
			{
				this.DistanceBasedPace(ply);
				bool flag2 = this.alertLevel >= this.sneakyAlertLevel;
				if (flag2)
				{
					this.DistanceBasedPace(ply);
					vector = this.getNearestPlayer(true).transform.position;
				}
				else
				{
					bool sneaking = this.alertLevel < this.sneakyAlertLevel;
					Vector3 pPos = ply.transform.position;
					Vector3 pFwd = ply.transform.forward;
					Vector3 pRight = ply.transform.right;
					PlayerControllerB watcher;
					float gazeAlign;
					bool flag3 = sneaking && this.spottedCooldown <= 0f && this.TryGetMostDangerousViewer(out watcher, out gazeAlign);
					if (flag3)
					{
						float distToWatcher = Vector3.Distance(base.transform.position, watcher.transform.position);
						bool flag4 = gazeAlign >= 0.6f && distToWatcher <= 20f;
						if (flag4)
						{
							Vector3 away = base.transform.position - watcher.transform.position;
							away.y = 0f;
							Vector3 biasPoint = base.transform.position + away.normalized * 8f;
							Vector3 retreat;
							bool flag5 = this.TryFindCoverAgainstPlayer(watcher, biasPoint, 16f, 1.2f, out retreat);
							if (flag5)
							{
								this.spottedCooldown = 3f;
								outScore = 5f;
								return retreat;
							}
							Vector3 groupRetreat;
							bool flag6 = this.TryFindGroupCover(biasPoint, 8f, 16f, 3, 16, out groupRetreat);
							if (flag6)
							{
								this.spottedCooldown = 3f;
								outScore = 4f;
								return groupRetreat;
							}
							Vector3 brute = base.transform.position + away.normalized * 6f;
							Vector3 bruteNav;
							bool flag7 = this.TrySampleNavmesh(brute, 3f, out bruteNav, -1);
							if (flag7)
							{
								this.spottedCooldown = 2f;
								outScore = 2f;
								return bruteNav;
							}
						}
					}
					Vector3 coverVsTarget;
					bool flag8 = sneaking && this.TryFindCoverAgainstPlayer(ply, pPos, 18f, 1.1f, out coverVsTarget);
					if (flag8)
					{
						outScore = 3.5f;
						this.DistanceBasedPace(ply);
						vector = coverVsTarget;
					}
					else
					{
						Vector3 groupCover;
						bool flag9 = sneaking && this.TryFindGroupCover(pPos, 1.1f, 14f, 3, 16, out groupCover);
						if (flag9)
						{
							outScore = 3f;
							this.DistanceBasedPace(ply);
							vector = groupCover;
						}
						else
						{
							float sneakRadiusNear = 7.5f;
							float sneakRadiusFar = 11f;
							float flankRadius = 9f;
							float closeRadius = 0f;
							float navSampleRange = 6f;
							List<Vector3> candidates = new List<Vector3>(8)
							{
								pPos - pFwd * sneakRadiusNear,
								pPos - pFwd * sneakRadiusFar,
								pPos - pFwd * flankRadius + pRight * (flankRadius * 0.75f),
								pPos - pFwd * flankRadius - pRight * (flankRadius * 0.75f),
								pPos + Quaternion.AngleAxis(35f, Vector3.up) * -pFwd * closeRadius,
								pPos + Quaternion.AngleAxis(-35f, Vector3.up) * -pFwd * closeRadius,
								pPos - pFwd * (closeRadius * 0.6f),
								pPos - pFwd * (closeRadius * 1.3f)
							};
							for (int i = 0; i < candidates.Count; i++)
							{
								List<Vector3> list = candidates;
								int num = i;
								list[num] += new Vector3(Random.Range(-0.75f, 0.75f), 0f, Random.Range(-0.75f, 0.75f));
							}
							float bestScore = float.NegativeInfinity;
							Vector3 bestPos = base.transform.position;
							foreach (Vector3 raw in candidates)
							{
								Vector3 navPos;
								bool flag10 = !this.TrySampleNavmesh(raw, navSampleRange, out navPos, -1);
								if (!flag10)
								{
									Vector3 fromPlayer = (navPos - pPos).normalized;
									float behind = Mathf.Clamp01(-Vector3.Dot(pFwd, fromPlayer));
									float ideal = (sneaking ? 9.5f : 6.5f);
									float d = Vector3.Distance(navPos, pPos);
									float distScore = 1f - Mathf.Clamp01(Mathf.Abs(d - ideal) / ideal);
									float cover = this.CoverScoreAllPlayers(navPos);
									float losScore = (sneaking ? cover : Mathf.Max(cover, -0.4f));
									bool candVisible = cover <= 0.1f;
									bool flag11 = Time.time - this.lastSeenTime < this.seenGrace && candVisible;
									if (flag11)
									{
										losScore -= 1.2f;
									}
									float myDist = Vector3.Distance(base.transform.position, navPos);
									float travelScore = 1f - Mathf.Clamp01(myDist / 30f);
									float lateral = Mathf.Abs(Vector3.Dot(fromPlayer, pRight));
									float lateralScore = (sneaking ? (0.15f * lateral) : (0.35f * lateral));
									float wBehind = (sneaking ? 1f : 0.6f);
									float score = wBehind * behind + 1f * distScore + 1.2f * losScore + 0.3f * travelScore + 0.4f * lateralScore;
									bool flag12 = score > bestScore;
									if (flag12)
									{
										bestScore = score;
										bestPos = navPos;
									}
								}
							}
							bool flag13 = bestScore == float.NegativeInfinity;
							if (flag13)
							{
								Vector3 j = pPos - base.transform.position;
								j.y = 0f;
								bool flag14 = j.sqrMagnitude > 0.01f;
								if (flag14)
								{
									j = Quaternion.AngleAxis(20f * ((Random.value > 0.5f) ? 1f : (-1f)), Vector3.up) * j.normalized * 4f;
									bool flag15 = !this.TrySampleNavmesh(base.transform.position + j, 6f, out bestPos, -1);
									if (flag15)
									{
										bestPos = base.transform.position;
									}
								}
							}
							this.DistanceBasedPace(ply);
							outScore = bestScore;
							vector = bestPos;
						}
					}
				}
			}
			return vector;
		}

		private bool TrySampleNavmesh(Vector3 pos, float maxDist, out Vector3 hitPos, int areaMask = -1)
		{
			NavMeshHit hit;
			bool flag = NavMesh.SamplePosition(pos, out hit, maxDist, areaMask);
			bool flag2;
			if (flag)
			{
				hitPos = hit.position;
				flag2 = true;
			}
			else
			{
				hitPos = Vector3.zero;
				flag2 = false;
			}
			return flag2;
		}

		private IEnumerable<PlayerControllerB> AllActivePlayers()
		{
			PlayerControllerB[] arr = RoundManager.Instance.playersManager.allPlayerScripts;
			int num;
			for (int i = 0; i < arr.Length; i = num + 1)
			{
				PlayerControllerB p = arr[i];
				bool flag = p != null && p.isPlayerControlled && !p.isPlayerDead;
				if (flag)
				{
					yield return p;
				}
				p = null;
				num = i;
			}
			yield break;
		}

		private bool PlayerHasLOSTo(Vector3 candidatePos, PlayerControllerB ply, LayerMask mask, float raise = 1.4f)
		{
			Vector3 from = ((ply.playerEye != null) ? ply.playerEye.transform.position : (ply.transform.position + Vector3.up * 1.6f));
			Vector3 to = candidatePos + Vector3.up * raise;
			RaycastHit raycastHit;
			return !Physics.Linecast(from, to, out raycastHit, mask, QueryTriggerInteraction.Ignore);
		}

		private bool IsVisibleToAnyPlayers(Vector3 pos)
		{
			foreach (PlayerControllerB p in RoundManager.Instance.playersManager.allPlayerScripts)
			{
				bool flag = p == null || !p.isPlayerControlled || p.isPlayerDead;
				if (!flag)
				{
					bool flag2 = !this.OccludedFromPlayer_Multi(pos, p);
					if (flag2)
					{
						return true;
					}
				}
			}
			return false;
		}

		private bool IsOccludedFromAllPlayers(Vector3 pos)
		{
			return !this.IsVisibleToAnyPlayers(pos);
		}

		private Vector3[] BuildPointsForCandidate(Vector3 candidatePos, PlayerControllerB ply)
		{
			ShamblerEnemy.<>c__DisplayClass132_0 CS$<>8__locals1;
			CS$<>8__locals1.candidatePos = candidatePos;
			Vector3 eye = ((ply != null && ply.playerEye != null) ? ply.playerEye.transform.position : ((ply != null) ? (ply.transform.position + Vector3.up * 1.6f) : (CS$<>8__locals1.candidatePos + Vector3.up * 1.6f)));
			CS$<>8__locals1.fwd = (CS$<>8__locals1.candidatePos - eye).normalized;
			bool flag = CS$<>8__locals1.fwd.sqrMagnitude < 0.0001f;
			if (flag)
			{
				CS$<>8__locals1.fwd = Vector3.forward;
			}
			CS$<>8__locals1.right = Vector3.Cross(Vector3.up, CS$<>8__locals1.fwd).normalized;
			CS$<>8__locals1.up = Vector3.Cross(CS$<>8__locals1.fwd, CS$<>8__locals1.right).normalized;
			Vector3 lFoot = (this.footLCol ? this.footLCol.localPosition : new Vector3(-0.25f, 0.1f, 0f));
			Vector3 rFoot = (this.footRCol ? this.footRCol.localPosition : new Vector3(0.25f, 0.1f, 0f));
			Vector3 lArm = (this.armLCol ? this.armLCol.localPosition : new Vector3(-0.45f, 1.2f, 0f));
			Vector3 rArm = (this.armRCol ? this.armRCol.localPosition : new Vector3(0.45f, 1.2f, 0f));
			Vector3 head = (this.headCol ? this.headCol.localPosition : new Vector3(0f, 1.7f, 0f));
			return new Vector3[]
			{
				ShamblerEnemy.<BuildPointsForCandidate>g__ToWorld|132_0(lFoot, ref CS$<>8__locals1),
				ShamblerEnemy.<BuildPointsForCandidate>g__ToWorld|132_0(rFoot, ref CS$<>8__locals1),
				ShamblerEnemy.<BuildPointsForCandidate>g__ToWorld|132_0(lArm, ref CS$<>8__locals1),
				ShamblerEnemy.<BuildPointsForCandidate>g__ToWorld|132_0(rArm, ref CS$<>8__locals1),
				ShamblerEnemy.<BuildPointsForCandidate>g__ToWorld|132_0(head, ref CS$<>8__locals1)
			};
		}

		private bool OccludedFromPlayer_Multi(Vector3 candidatePos, PlayerControllerB ply)
		{
			bool flag = ply == null;
			bool flag2;
			if (flag)
			{
				flag2 = true;
			}
			else
			{
				Vector3 eye = ((ply.playerEye != null) ? ply.playerEye.transform.position : (ply.transform.position + Vector3.up * 1.6f));
				Vector3[] targets = this.BuildPointsForCandidate(candidatePos, ply);
				int blocked = 0;
				for (int i = 0; i < targets.Length; i++)
				{
					bool flag3 = Physics.Linecast(eye, targets[i], this.WorldMask, QueryTriggerInteraction.Ignore);
					if (flag3)
					{
						blocked++;
					}
				}
				float frac = (float)blocked / (float)targets.Length;
				flag2 = frac >= this.coverRequiredFraction;
			}
			return flag2;
		}

		public void baseClosingDistance()
		{
			this.lookStep();
			this.agent.speed = 6f * Plugin.moaiGlobalSpeed.Value;
			bool flag = this.agent.velocity.magnitude > this.agent.speed / 4f;
			if (flag)
			{
				bool isServer = RoundManager.Instance.IsServer;
				if (isServer)
				{
					this.DoAnimationClientRpc(1);
				}
				this.setAnimationSpeedClientRpc(this.agent.velocity.magnitude / this.walkAnimationCoefficient);
			}
			else
			{
				bool flag2 = this.agent.velocity.magnitude <= this.agent.speed / 8f;
				if (flag2)
				{
					bool isServer2 = RoundManager.Instance.IsServer;
					if (isServer2)
					{
						this.DoAnimationClientRpc(0);
					}
					this.setAnimationSpeedClientRpc(1f);
				}
			}
			PlayerControllerB ply = ((this.targetPlayer != null && base.PlayerIsTargetable(this.targetPlayer, false, false)) ? this.targetPlayer : this.getNearestPlayer(true));
			bool flag3 = ply == null || this.getNearestPlayer(true) == null;
			if (flag3)
			{
				this.Think("No valid player; switching to Search.");
				this.usingCustomGoal = false;
				base.StartSearch(base.transform.position, null);
				base.SwitchToBehaviourClientRpc(0);
			}
			else
			{
				bool seenNow = this.IsVisibleToAnyPlayers(base.transform.position);
				bool flag4 = seenNow;
				if (flag4)
				{
					this.lastSeenTime = Time.time;
				}
				bool flag5 = this.alertLevel >= 70f && (double)Vector3.Distance(this.getNearestPlayer(true).transform.position, base.transform.position) < (double)this.maxLeapDistance / 1.5;
				if (flag5)
				{
					base.SwitchToBehaviourClientRpc(2);
					this.crouchTimer = (float)(0.5 + this.enemyRandom.NextDouble() * 1.5);
					this.crouchTimeout = 7f;
				}
				else
				{
					bool flag6 = this.alertLevel <= 70f && Vector3.Distance(this.getNearestPlayer(true).transform.position, base.transform.position) < this.maxStabDistance;
					if (flag6)
					{
						this.doneStab = false;
						this.isStabbing = false;
						base.SwitchToBehaviourClientRpc(8);
					}
					else
					{
						bool inCoverNow = !seenNow;
						bool flag7 = inCoverNow && Time.time < this.coverLockUntil;
						if (flag7)
						{
							this.usingCustomGoal = true;
							this.movingTowardsTargetPlayer = false;
							bool flag8 = this.stickyGoal != Vector3.zero && this.agent.destination != this.stickyGoal;
							if (flag8)
							{
								this.agent.SetDestination(this.stickyGoal);
							}
						}
						else
						{
							float distNow = ((this.stickyGoal == Vector3.zero) ? 9999f : Vector3.Distance(base.transform.position, this.stickyGoal));
							float speed = this.agent.velocity.magnitude;
							bool flag9 = speed < this.stuckSpeedThresh && distNow > this.goalArrivalTol;
							if (flag9)
							{
								this.stuckTimer += Time.deltaTime;
							}
							else
							{
								this.stuckTimer = 0f;
							}
							bool arrived = this.ReachedGoal(this.stickyGoal);
							bool stuck = this.stuckTimer >= this.stuckSeconds;
							bool needNew = arrived || stuck || this.PathBadOrPartial() || this.stickyGoal == Vector3.zero;
							bool consider = needNew || this.CooldownsAllowRepath();
							bool flag10 = consider;
							if (flag10)
							{
								float newScore;
								Vector3 candidate = this.chooseTravelGoal(out newScore);
								bool better = newScore > this.stickyScore + this.goalBetterBy;
								bool candidateIsVisible = this.IsVisibleToAnyPlayers(candidate);
								bool flag11 = candidateIsVisible && Time.time - this.lastSeenTime < this.seenGrace;
								if (flag11)
								{
									better = newScore > this.stickyScore + (this.goalBetterBy + 1f);
								}
								bool flag12 = needNew || better;
								if (flag12)
								{
									bool flag13 = this.IsOccludedFromAllPlayers(candidate);
									if (flag13)
									{
										this.coverLockUntil = Time.time + this.coverDwellSeconds;
									}
									else
									{
										this.coverLockUntil = -999f;
									}
									this.stickyScore = newScore;
									this.stickyPickedAt = Time.time;
									this.ApplyStickyDestination(candidate);
								}
								else
								{
									this.usingCustomGoal = true;
									this.movingTowardsTargetPlayer = false;
								}
							}
							else
							{
								this.usingCustomGoal = true;
								this.movingTowardsTargetPlayer = false;
							}
							bool flag14 = this.stickyGoal != Vector3.zero && this.agent.destination != this.stickyGoal;
							if (flag14)
							{
								this.agent.SetDestination(this.stickyGoal);
							}
							this.lastGoalDist = distNow;
							bool flag15 = ply == null || Vector3.Distance(base.transform.position, ply.transform.position) > 62f || this.stamina <= 0f;
							if (flag15)
							{
								this.Think("Exiting sneak (reason=" + ((ply == null) ? "no target" : ((this.stamina <= 0f) ? "tired" : "too far")) + ")");
								this.targetPlayer = null;
								this.usingCustomGoal = false;
								this.stickyGoal = Vector3.zero;
								this.stickyScore = float.NegativeInfinity;
								base.SwitchToBehaviourClientRpc(0);
							}
						}
					}
				}
			}
		}

		public void lookStep()
		{
			PlayerControllerB[] players = RoundManager.Instance.playersManager.allPlayerScripts;
			float maxAlertUpdate = 0f;
			foreach (PlayerControllerB ply in players)
			{
				bool flag = ply == null || ply.isPlayerDead || !ply.isPlayerControlled || !this.PlayerQualifies(ply);
				if (!flag)
				{
					Vector3 dirVec = (base.transform.position - ply.transform.position).normalized;
					Vector3 playerLookVec = ply.playerEye.transform.forward.normalized;
					float localAlertLevel = Vector3.Dot(playerLookVec, dirVec) * Math.Max(25f - Vector3.Distance(base.transform.position, ply.transform.position), 0f);
					bool flag2 = localAlertLevel > maxAlertUpdate;
					if (flag2)
					{
						maxAlertUpdate = localAlertLevel;
					}
				}
			}
			bool flag3 = maxAlertUpdate > 0f;
			if (flag3)
			{
				this.alertLevel += maxAlertUpdate * this.alertRate;
			}
		}

		public void baseSearchingForPlayer(float lineOfSightRange = 62f)
		{
			this.agent.speed = 6f * Plugin.moaiGlobalSpeed.Value;
			this.agent.angularSpeed = 120f;
			bool flag = this.sourcecycle % 5 == 0;
			if (flag)
			{
				this.targetPlayer = null;
			}
			bool flag2 = this.agent.velocity.magnitude > this.agent.speed / 4f;
			if (flag2)
			{
				bool isServer = RoundManager.Instance.IsServer;
				if (isServer)
				{
					this.DoAnimationClientRpc(1);
				}
				this.setAnimationSpeedClientRpc(this.agent.velocity.magnitude / this.walkAnimationCoefficient);
			}
			else
			{
				bool flag3 = this.agent.velocity.magnitude <= this.agent.speed / 8f;
				if (flag3)
				{
					bool isServer2 = RoundManager.Instance.IsServer;
					if (isServer2)
					{
						this.DoAnimationClientRpc(0);
					}
					this.setAnimationSpeedClientRpc(1f);
				}
			}
			bool flag4 = !this.creatureVoice.isPlaying;
			if (flag4)
			{
				this.moaiSoundPlayClientRpc("creatureVoice");
			}
			this.updateEntranceChance();
			bool flag5 = this.enemyRandom.NextDouble() < (double)this.chanceToLocateEntrance && base.gameObject.transform.localScale.x <= 2.2f && Plugin.canEnterIndoors.Value;
			if (flag5)
			{
				this.Think("Switching to HeadingToEntrance.");
				base.StopSearch(this.currentSearch, true);
				base.SwitchToBehaviourClientRpc(1);
			}
			bool flag6 = (this.FoundClosestPlayerInRange(lineOfSightRange, true) && this.stamina >= 120f) || this.provokePoints > 0;
			if (flag6)
			{
				this.Think("Found player for ClosingDistance.");
				base.StopSearch(this.currentSearch, true);
				base.SwitchToBehaviourClientRpc(4);
			}
			else
			{
				bool flag7 = this.stamina < 100f;
				if (flag7)
				{
					this.targetPlayer = null;
				}
				bool flag8 = this.stabbedCapturedPlayer;
				if (flag8)
				{
					this.capturedPlayer = null;
					this.headingToNestDistance = 9999f;
					this.headingToNestTimeout = 6f;
					base.SwitchToBehaviourClientRpc(6);
					base.StopSearch(this.currentSearch, true);
				}
			}
		}

		public void baseHeadingToNest()
		{
			this.agent.speed = 6f * Plugin.moaiGlobalSpeed.Value;
			this.agent.angularSpeed = 120f;
			this.headingToNestTimeout -= 0.2f;
			bool flag = this.headingToNestTimeout < 0f;
			if (flag)
			{
				bool flag2 = this.headingToNestDistance > Vector3.Distance(base.transform.position, this.nestSpot);
				if (flag2)
				{
					this.isPlanting = false;
					this.donePlant = false;
					base.SwitchToBehaviourClientRpc(7);
				}
				else
				{
					this.headingToNestTimeout = 6f;
					this.headingToNestDistance = Vector3.Distance(base.transform.position, this.nestSpot);
				}
			}
			bool flag3 = this.agent.velocity.magnitude > this.agent.speed / 4f;
			if (flag3)
			{
				bool isServer = RoundManager.Instance.IsServer;
				if (isServer)
				{
					this.DoAnimationClientRpc(1);
				}
				this.setAnimationSpeedClientRpc(this.agent.velocity.magnitude / this.walkAnimationCoefficient);
			}
			else
			{
				bool flag4 = this.agent.velocity.magnitude <= this.agent.speed / 8f;
				if (flag4)
				{
					bool isServer2 = RoundManager.Instance.IsServer;
					if (isServer2)
					{
						this.DoAnimationClientRpc(0);
					}
					this.setAnimationSpeedClientRpc(1f);
				}
			}
			base.SetDestinationToPosition(this.nestSpot, false);
			bool flag5 = Vector3.Distance(base.transform.position, this.nestSpot) < 15f;
			if (flag5)
			{
				this.isPlanting = false;
				this.donePlant = false;
				base.SwitchToBehaviourClientRpc(7);
			}
			bool flag6 = !this.stabbedCapturedPlayer;
			if (flag6)
			{
				this.isStabbing = false;
				this.doneStab = false;
				base.StartSearch(base.transform.position, null);
				base.SwitchToBehaviourClientRpc(0);
			}
			bool flag7 = !this.isOutside;
			if (flag7)
			{
				base.SwitchToBehaviourClientRpc(1);
			}
		}

		public void basePlantingStake()
		{
			this.agent.speed = 0f;
			this.setAnimationSpeedClientRpc(1f);
			bool isEnemyDead = this.isEnemyDead;
			if (isEnemyDead)
			{
				this.agent.updateRotation = true;
				this.isStabbing = false;
				this.doneStab = false;
				this.isPlanting = false;
				this.donePlant = false;
				base.SwitchToBehaviourClientRpc(0);
				base.StartSearch(base.transform.position, null);
				this.Think("Switched to searching for player");
			}
			bool flag = !this.isPlanting;
			if (flag)
			{
				base.StartCoroutine(this.DoPlant());
				this.isPlanting = true;
			}
			else
			{
				bool flag2 = this.donePlant;
				if (flag2)
				{
					this.agent.updateRotation = true;
					this.isPlanting = false;
					this.donePlant = false;
					this.animPlayClientRpc("Idle");
					base.StartSearch(base.transform.position, null);
					base.SwitchToBehaviourClientRpc(0);
					base.StartSearch(base.transform.position, null);
					this.Think("Switched to searching for player");
				}
			}
		}

		public IEnumerator DoPlant()
		{
			bool flag = this.plantTime + this.plantCooldown > Time.time;
			if (flag)
			{
				this.agent.updateRotation = true;
				this.isPlanting = false;
				this.donePlant = false;
				base.SwitchToBehaviourClientRpc(0);
				base.StartSearch(base.transform.position, null);
				this.Think("Switched to searching for player");
				yield break;
			}
			this.DoAnimationClientRpc(6);
			this.animPlayClientRpc("PlantStake");
			this.moaiSoundPlayClientRpc("creaturePlant");
			this.plantTime = Time.time;
			bool flag2 = this.stabbedPlayer;
			if (flag2)
			{
				this.SetColliderClientRpc(this.stabbedPlayer.NetworkObject.NetworkObjectId, false);
			}
			yield return new WaitForSeconds(1.05f);
			PlayerControllerB victim = ((this.capturedPlayer != null) ? this.capturedPlayer : this.stabbedPlayer);
			try
			{
				bool isHost = RoundManager.Instance.IsHost;
				if (isHost)
				{
					GameObject stakePrefab = Plugin.ShamblerStakePrefab;
					GameObject stake = Object.Instantiate<GameObject>(stakePrefab, this.heldStakeRef.transform.position, this.heldStakeRef.transform.rotation);
					ShamblerStake obj = stake.GetComponent<ShamblerStake>();
					NetworkObject netObj = stake.GetComponent<NetworkObject>();
					obj.owner = this;
					obj.victim = victim;
					obj.SetVictimClientRpc(victim.NetworkObject.NetworkObjectId);
					bool flag3 = !netObj.IsSpawned;
					if (flag3)
					{
						netObj.Spawn(false);
					}
					ShamblerEnemy.stuckPlayerIds.Add(victim.NetworkObject.NetworkObjectId);
					stakePrefab = null;
					stake = null;
					obj = null;
					netObj = null;
				}
				bool flag4 = victim != null;
				if (flag4)
				{
					this.letGoOfPlayerClientRpc(victim.NetworkObject.NetworkObjectId);
				}
				bool flag5 = this.stabbedPlayer != null;
				if (flag5)
				{
					this.SetStabbedPlayerClientRpc(0UL, true);
				}
				bool flag6 = this.capturedPlayer != null;
				if (flag6)
				{
					this.SetCapturedPlayerClientRpc(0UL, true);
				}
				this.stabbedPlayer = null;
				this.capturedPlayer = null;
				this.stabbedCapturedPlayer = false;
				this.targetPlayer = null;
				this.mostRecentPlayer = null;
				this.usingCustomGoal = false;
				this.stickyGoal = Vector3.zero;
				this.stickyScore = float.NegativeInfinity;
			}
			catch (Exception ex2)
			{
				Exception ex = ex2;
				Debug.LogError(string.Format("[Shambler] DoPlant exception: {0}", ex));
			}
			yield return new WaitForSeconds(0.5f);
			this.donePlant = true;
			this.isPlanting = false;
			this.Think("Plant Done");
			yield return new WaitForSeconds(1f);
			this.SetColliderClientRpc(victim.NetworkObject.NetworkObjectId, true);
			yield break;
		}

		public void baseHeadingToEntrance()
		{
			this.targetPlayer = null;
			bool flag = this.agent.velocity.magnitude > this.agent.speed / 4f;
			if (flag)
			{
				bool isServer = RoundManager.Instance.IsServer;
				if (isServer)
				{
					this.DoAnimationClientRpc(1);
				}
				this.setAnimationSpeedClientRpc(this.agent.velocity.magnitude / this.walkAnimationCoefficient);
			}
			else
			{
				bool flag2 = this.agent.velocity.magnitude <= this.agent.speed / 8f;
				if (flag2)
				{
					bool isServer2 = RoundManager.Instance.IsServer;
					if (isServer2)
					{
						this.DoAnimationClientRpc(0);
					}
					this.setAnimationSpeedClientRpc(1f);
				}
			}
			base.SetDestinationToPosition(this.nearestEntranceNavPosition, false);
			bool flag3 = this.isOutside != this.nearestEntrance.isEntranceToBuilding || this.agent.pathStatus == NavMeshPathStatus.PathPartial;
			if (flag3)
			{
				this.entranceDelay = 150;
				base.StartSearch(base.transform.position, null);
				base.SwitchToBehaviourClientRpc(0);
			}
			bool flag4 = (double)Vector3.Distance(base.transform.position, this.nearestEntranceNavPosition) < 2.0 + (double)base.gameObject.transform.localScale.x;
			if (flag4)
			{
				bool isEntranceToBuilding = this.nearestEntrance.isEntranceToBuilding;
				if (isEntranceToBuilding)
				{
					Debug.Log("SHAMBLER: Warp in");
					EntityWarp.SendEnemyInside(this);
					this.nearestEntrance.PlayAudioAtTeleportPositions();
				}
				else
				{
					Debug.Log("SHAMBLER: Warp out");
					EntityWarp.SendEnemyOutside(this, true);
					this.nearestEntrance.PlayAudioAtTeleportPositions();
				}
				this.entranceDelay = 150;
				base.StartSearch(base.transform.position, null);
				base.SwitchToBehaviourClientRpc(0);
			}
			bool flag5 = this.provokePoints > 0;
			if (flag5)
			{
				base.StartSearch(base.transform.position, null);
				base.SwitchToBehaviourClientRpc(0);
			}
		}

		public void updateEntranceChance()
		{
			bool flag = !this.nearestEntrance;
			if (!flag)
			{
				float dist = Vector3.Distance(base.transform.position, this.nearestEntrance.transform.position);
				this.chanceToLocateEntrancePlayerBonus = 1f;
				bool flag2 = this.mostRecentPlayer;
				if (flag2)
				{
					bool flag3 = this.mostRecentPlayer == this.isOutside;
					if (flag3)
					{
						this.chanceToLocateEntrancePlayerBonus = 1f;
					}
					else
					{
						this.chanceToLocateEntrancePlayerBonus = 1.5f;
					}
				}
				int m = 1;
				bool flag4 = dist < 20f;
				if (flag4)
				{
					m = 4;
				}
				bool flag5 = dist < 15f;
				if (flag5)
				{
					m = 6;
				}
				bool flag6 = dist < 10f;
				if (flag6)
				{
					m = 7;
				}
				bool flag7 = dist < 5f;
				if (flag7)
				{
					m = 10;
				}
				bool flag8 = this.nearestEntrance;
				if (flag8)
				{
					this.chanceToLocateEntrance = (float)(1.0 / Math.Pow((double)dist, 2.0)) * (float)m * this.chanceToLocateEntrancePlayerBonus - (float)this.entranceDelay;
				}
			}
		}

		public bool FoundClosestPlayerInRange(float r, bool needLineOfSight)
		{
			bool flag = this.recovering;
			bool flag2;
			if (flag)
			{
				flag2 = false;
			}
			else
			{
				this.moaiTargetClosestPlayer(r, needLineOfSight);
				flag2 = this.PlayerQualifies(this.targetPlayer);
			}
			return flag2;
		}

		public bool moaiTargetClosestPlayer(float range, bool requireLineOfSight)
		{
			bool flag = this.recovering;
			bool flag2;
			if (flag)
			{
				flag2 = false;
			}
			else
			{
				this.mostOptimalDistance = range;
				PlayerControllerB playerControllerB = this.targetPlayer;
				this.targetPlayer = null;
				for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
				{
					bool flag3 = base.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i], false, false) && (!requireLineOfSight || base.CheckLineOfSightForPosition(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, Plugin.LOSWidth.Value, 80, -1f, null));
					if (flag3)
					{
						this.tempDist = Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
						bool flag4 = this.tempDist < this.mostOptimalDistance;
						if (flag4)
						{
							this.mostOptimalDistance = this.tempDist;
							bool flag5 = this.RageTarget != null && this.PlayerQualifies(StartOfRound.Instance.allPlayerScripts[i]) && this.RageTarget == StartOfRound.Instance.allPlayerScripts[i];
							if (flag5)
							{
								this.targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
								return true;
							}
							bool flag6 = this.PlayerQualifies(StartOfRound.Instance.allPlayerScripts[i]);
							if (flag6)
							{
								this.targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
							}
						}
					}
				}
				bool flag7 = this.targetPlayer != null && playerControllerB != null;
				if (flag7)
				{
					this.targetPlayer = playerControllerB;
				}
				flag2 = this.PlayerQualifies(this.targetPlayer);
			}
			return flag2;
		}

		public bool PlayerQualifies(PlayerControllerB ply)
		{
			bool flag = ply == null || ply.NetworkObject == null;
			bool flag2;
			if (flag)
			{
				flag2 = false;
			}
			else
			{
				bool isInHangarShipRoom = ply.isInHangarShipRoom;
				if (isInHangarShipRoom)
				{
					flag2 = false;
				}
				else
				{
					bool flag3 = this.EscapingEmployees.Contains(ply.NetworkObject.NetworkObjectId);
					if (flag3)
					{
						Vector3 enemyEye = (this.headCol ? this.headCol.position : (base.transform.position + Vector3.up * 1.6f));
						Vector3 playerEye = (ply.playerEye ? ply.playerEye.transform.position : (ply.transform.position + Vector3.up * 1.6f));
						enemyEye.y += 0.6f;
						playerEye.y += 0.6f;
						Vector3 toP = ply.transform.position - base.transform.position;
						toP.y = 0f;
						float dist = toP.magnitude;
						bool fovOk = dist <= 8f || (toP.sqrMagnitude > 0.001f && Vector3.Dot(base.transform.forward, toP.normalized) >= Mathf.Cos(1.2217305f));
						bool hasLOS = !Physics.Linecast(enemyEye, playerEye, this.WorldMask, QueryTriggerInteraction.Ignore);
						bool flag4 = fovOk && hasLOS;
						if (flag4)
						{
							this.alertLevel = 100f;
							this.RageTarget = ply;
							this.Think(string.Format("⚠ Rage: escaping {0} dist={1:F1}m FOV={2} LOS={3}", new object[] { ply.playerUsername, dist, fovOk, hasLOS }));
							this.ClearRageTarget();
							return true;
						}
					}
					bool flag5 = ply == this.RageTarget;
					if (flag5)
					{
						flag2 = true;
					}
					else
					{
						bool flag6 = ply == this.capturedPlayer || ply == this.stabbedPlayer;
						if (flag6)
						{
							flag2 = false;
						}
						else
						{
							bool flag7 = ply == this.capturedPlayer || ply == this.stabbedPlayer;
							if (flag7)
							{
								flag2 = false;
							}
							else
							{
								bool flag8 = ShamblerEnemy.IsPlayerStaked(ply);
								if (flag8)
								{
									flag2 = false;
								}
								else
								{
									bool flag9 = ply.isPlayerDead || !ply.isPlayerControlled;
									flag2 = !flag9;
								}
							}
						}
					}
				}
			}
			return flag2;
		}

		public async void ClearRageTarget()
		{
			await Task.Delay(10000);
			this.RageTarget = null;
			this.EscapingEmployees.Clear();
		}

		public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
		{
			base.HitEnemy(force, playerWhoHit, playHitSFX, -1);
			bool isEnemyDead = this.isEnemyDead;
			if (!isEnemyDead)
			{
				bool flag = hitID == -1 && playerWhoHit == null;
				if (!flag)
				{
					bool flag2 = playerWhoHit != null;
					if (flag2)
					{
						ulong pid = playerWhoHit.NetworkObject.NetworkObjectId;
						bool flag3 = this.stabbedPlayer != null && pid == this.stabbedPlayer.NetworkObject.NetworkObjectId;
						if (flag3)
						{
							return;
						}
						bool flag4 = this.capturedPlayer != null && pid == this.capturedPlayer.NetworkObject.NetworkObjectId;
						if (flag4)
						{
							return;
						}
					}
					this.enemyHP -= force;
					bool flag5 = playerWhoHit != null;
					if (flag5)
					{
						this.provokePoints += 20 * force;
						this.targetPlayer = playerWhoHit;
					}
					this.stamina = 120f;
					this.recovering = false;
					bool isOwner = base.IsOwner;
					if (isOwner)
					{
						bool flag6 = this.enemyHP <= 0 && !this.markDead;
						if (flag6)
						{
							base.KillEnemyOnOwnerClient(false);
							this.stopAllSound();
							this.isEnemyDead = true;
							this.moaiSoundPlayClientRpc("creatureDeath");
							this.deadEventClientRpc();
							this.markDead = true;
						}
						else
						{
							this.moaiSoundPlayClientRpc("creatureHit");
						}
					}
				}
			}
		}

		[ClientRpc]
		public void deadEventClientRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(2229496956U, clientRpcParams, RpcDelivery.Reliable);
				base.__endSendClientRpc(ref fastBufferWriter, 2229496956U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.animator.Play("Death");
			bool flag = !this.creatureDeath.isPlaying;
			if (flag)
			{
				this.creatureDeath.Play();
			}
			this.isEnemyDead = true;
		}

		[ServerRpc(RequireOwnership = false)]
		public void letGoOfPlayerServerRpc(ulong playerId)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
			{
				ServerRpcParams serverRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendServerRpc(2192772121U, serverRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerId);
				base.__endSendServerRpc(ref fastBufferWriter, 2192772121U, serverRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsServer && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.letGoOfPlayerClientRpc(playerId);
		}

		[ClientRpc]
		public void letGoOfPlayerClientRpc(ulong playerId)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(160126077U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerId);
				base.__endSendClientRpc(ref fastBufferWriter, 160126077U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			PlayerControllerB targetPlayer = null;
			RoundManager i = RoundManager.Instance;
			foreach (PlayerControllerB player in i.playersManager.allPlayerScripts)
			{
				bool flag = player.NetworkObject.NetworkObjectId == playerId;
				if (flag)
				{
					targetPlayer = player;
				}
			}
			NavMeshHit hit;
			bool flag2 = NavMesh.SamplePosition(targetPlayer.transform.position, out hit, 15f, -1);
			if (flag2)
			{
				targetPlayer.transform.position = hit.position;
			}
			bool flag3 = this.stabbedPlayer != null;
			if (flag3)
			{
				this.SetStabbedPlayerClientRpc(0UL, true);
			}
			bool flag4 = this.capturedPlayer != null;
			if (flag4)
			{
				this.SetCapturedPlayerClientRpc(0UL, true);
			}
			this.capturedPlayer = null;
			this.stabbedPlayer = null;
		}

		public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
		{
			bool flag = this.timeSinceHittingLocalPlayer < 0.5f || collidedEnemy.isEnemyDead || this.isEnemyDead;
			if (!flag)
			{
				bool flag2 = collidedEnemy.enemyType == this.enemyType;
				if (!flag2)
				{
					string nam = collidedEnemy.enemyType.name.ToLower();
					bool flag3 = nam.Contains("mouth") && nam.Contains("dog");
					if (!flag3)
					{
						bool flag4 = collidedEnemy.enemyType.enemyName.ToLower().Contains("soul");
						if (!flag4)
						{
							this.timeSinceHittingLocalPlayer = 0f;
							collidedEnemy.HitEnemy(1, null, true, -1);
						}
					}
				}
			}
		}

		public override void OnCollideWithPlayer(Collider other)
		{
			bool flag = this.timeSinceHittingLocalPlayer < 0.6f;
			if (!flag)
			{
				PlayerControllerB pcb = base.MeetsStandardPlayerCollisionConditions(other, false, false);
				bool flag2 = pcb;
				if (flag2)
				{
					this.DmgPlayerClientRpc(pcb.NetworkObject.NetworkObjectId, 60);
				}
			}
		}

		[ServerRpc(RequireOwnership = false)]
		public void attachPlayerServerRpc(ulong uid, bool lastHit, int staminaGrant)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
			{
				ServerRpcParams serverRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendServerRpc(836769684U, serverRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, uid);
				fastBufferWriter.WriteValueSafe<bool>(in lastHit, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(fastBufferWriter, staminaGrant);
				base.__endSendServerRpc(ref fastBufferWriter, 836769684U, serverRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsServer && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.attachPlayerClientRpc(uid, lastHit, staminaGrant);
		}

		[ClientRpc]
		public void attachPlayerClientRpc(ulong uid, bool lastHit, int staminaGrant)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(1109110496U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, uid);
				fastBufferWriter.WriteValueSafe<bool>(in lastHit, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(fastBufferWriter, staminaGrant);
				base.__endSendClientRpc(ref fastBufferWriter, 1109110496U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.stamina += (float)staminaGrant;
			RoundManager i = RoundManager.Instance;
			foreach (PlayerControllerB player in i.playersManager.allPlayerScripts)
			{
				bool flag = player.NetworkObject.NetworkObjectId == uid;
				if (flag)
				{
					bool flag2 = !lastHit;
					if (flag2)
					{
						player.transform.position = this.capturePoint.position;
						this.capturedPlayer = player;
					}
					else
					{
						player.deadBody.transform.position = this.capturePoint.position;
						this.capturedPlayer = player;
					}
					break;
				}
			}
		}

		[ClientRpc]
		public void attachPlayerSpikeClientRpc(ulong uid, bool lastHit, int staminaGrant)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(2790292547U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, uid);
				fastBufferWriter.WriteValueSafe<bool>(in lastHit, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(fastBufferWriter, staminaGrant);
				base.__endSendClientRpc(ref fastBufferWriter, 2790292547U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.stamina += (float)staminaGrant;
			RoundManager i = RoundManager.Instance;
			foreach (PlayerControllerB player in i.playersManager.allPlayerScripts)
			{
				bool flag = player.NetworkObject.NetworkObjectId == uid;
				if (flag)
				{
					bool flag2 = !lastHit;
					if (flag2)
					{
						player.transform.position = this.capturePoint.position;
						this.capturedPlayer = player;
						bool flag3 = this.capturedPlayer != null;
						if (flag3)
						{
							this.SetCapturedPlayerClientRpc(this.capturedPlayer.NetworkObject.NetworkObjectId, false);
						}
					}
					else
					{
						player.deadBody.transform.position = this.capturePoint.position;
						this.capturedPlayer = player;
						bool flag4 = this.capturedPlayer != null;
						if (flag4)
						{
							this.SetCapturedPlayerClientRpc(this.capturedPlayer.NetworkObject.NetworkObjectId, false);
						}
					}
					break;
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
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(1314365859U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, playerid);
				fastBufferWriter.WriteValueSafe<bool>(in value, default(FastBufferWriter.ForPrimitives));
				base.__endSendClientRpc(ref fastBufferWriter, 1314365859U, clientRpcParams, RpcDelivery.Reliable);
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

		public virtual void playSoundId(string id)
		{
		}

		public void stopAllSound()
		{
			this.creatureSFX.Stop();
			this.creatureVoice.Stop();
			this.creatureAnger.Stop();
			this.creatureLaugh.Stop();
			this.creatureLeapLand.Stop();
		}

		[ClientRpc]
		public void moaiSoundPlayClientRpc(string soundName)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(1531827138U, clientRpcParams, RpcDelivery.Reliable);
				bool flag = soundName != null;
				fastBufferWriter.WriteValueSafe<bool>(in flag, default(FastBufferWriter.ForPrimitives));
				if (flag)
				{
					fastBufferWriter.WriteValueSafe(soundName, false);
				}
				base.__endSendClientRpc(ref fastBufferWriter, 1531827138U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			uint num = <PrivateImplementationDetails>.ComputeStringHash(soundName);
			if (num <= 1359779715U)
			{
				if (num <= 606747055U)
				{
					if (num != 533825425U)
					{
						if (num == 606747055U)
						{
							if (soundName == "creatureAnger")
							{
								this.stopAllSound();
								this.creatureAnger.Play();
							}
						}
					}
					else if (soundName == "creatureLaugh")
					{
						this.stopAllSound();
						this.creatureLaugh.Play();
					}
				}
				else if (num != 1331651866U)
				{
					if (num != 1342519341U)
					{
						if (num == 1359779715U)
						{
							if (soundName == "creatureLeapLand")
							{
								this.stopAllSound();
								this.creatureLeapLand.Play();
							}
						}
					}
					else if (soundName == "creaturePlant")
					{
						this.creaturePlant.Play();
					}
				}
				else if (soundName == "creatureDeath")
				{
					bool flag2 = !this.creatureDeath.isPlaying;
					if (flag2)
					{
						this.stopAllSound();
						this.creatureDeath.Play();
					}
				}
			}
			else if (num <= 1982908669U)
			{
				if (num != 1737222722U)
				{
					if (num != 1865690839U)
					{
						if (num == 1982908669U)
						{
							if (soundName == "creatureHit")
							{
								this.creatureTakeDmg.Play();
							}
						}
					}
					else if (soundName == "creatureSFX")
					{
						this.stopAllSound();
						this.creatureSFX.Play();
					}
				}
				else if (soundName == "creatureVoice")
				{
					this.stopAllSound();
					double[] timeIntervals = new double[] { 0.0, 0.8244, 11.564, 29.11, 34.491, 37.84, 48.689, 64.518, 89.535, 92.111 };
					int selectedTime = Random.Range(0, timeIntervals.Length);
					this.creatureVoice.Play();
					this.creatureVoice.SetScheduledStartTime(timeIntervals[selectedTime]);
					this.creatureVoice.time = (float)timeIntervals[selectedTime];
				}
			}
			else if (num != 3057030647U)
			{
				if (num != 3196437896U)
				{
					if (num == 3343129103U)
					{
						if (soundName == "step")
						{
							bool flag3 = this.creatureSteps.Length != 0;
							if (flag3)
							{
								int selectedIndex = this.enemyRandom.Next(0, this.creatureSteps.Length);
								this.creatureSteps[selectedIndex].Play();
							}
						}
					}
				}
				else if (soundName == "creatureStab")
				{
					this.stopAllSound();
					this.creatureStab.Play();
				}
			}
			else if (soundName == "creatureSneakyStab")
			{
				this.stopAllSound();
				this.creatureSneakyStab.Play();
			}
		}

		[ClientRpc]
		public void setAnimationSpeedClientRpc(float speed)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(2582106502U, clientRpcParams, RpcDelivery.Reliable);
				fastBufferWriter.WriteValueSafe<float>(in speed, default(FastBufferWriter.ForPrimitives));
				base.__endSendClientRpc(ref fastBufferWriter, 2582106502U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.animator.speed = speed;
		}

		[ClientRpc]
		public void DoAnimationClientRpc(int index)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(1163343604U, clientRpcParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(fastBufferWriter, index);
				base.__endSendClientRpc(ref fastBufferWriter, 1163343604U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			bool flag = this.animator;
			if (flag)
			{
				this.animator.SetInteger("state", index);
			}
		}

		[ClientRpc]
		public void animPlayClientRpc(string name)
		{
			NetworkManager networkManager = base.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams;
				FastBufferWriter fastBufferWriter = base.__beginSendClientRpc(3587374905U, clientRpcParams, RpcDelivery.Reliable);
				bool flag = name != null;
				fastBufferWriter.WriteValueSafe<bool>(in flag, default(FastBufferWriter.ForPrimitives));
				if (flag)
				{
					fastBufferWriter.WriteValueSafe(name, false);
				}
				base.__endSendClientRpc(ref fastBufferWriter, 3587374905U, clientRpcParams, RpcDelivery.Reliable);
			}
			if (this.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || (!networkManager.IsClient && !networkManager.IsHost))
			{
				return;
			}
			this.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
			this.animator.Play(name);
		}

		[CompilerGenerated]
		internal static Vector3 <BuildPointsForCandidate>g__ToWorld|132_0(Vector3 lp, ref ShamblerEnemy.<>c__DisplayClass132_0 A_1)
		{
			return A_1.candidatePos + A_1.right * lp.x + A_1.up * lp.y + A_1.fwd * lp.z;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__registerRpc(2925436343U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_2925436343), "SetCapturedPlayerClientRpc");
			base.__registerRpc(3110006365U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_3110006365), "SetStabbedPlayerClientRpc");
			base.__registerRpc(3897930302U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_3897930302), "DmgPlayerClientRpc");
			base.__registerRpc(2556090093U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_2556090093), "DoGroundHopClientRpc");
			base.__registerRpc(2229496956U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_2229496956), "deadEventClientRpc");
			base.__registerRpc(2192772121U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_2192772121), "letGoOfPlayerServerRpc");
			base.__registerRpc(160126077U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_160126077), "letGoOfPlayerClientRpc");
			base.__registerRpc(836769684U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_836769684), "attachPlayerServerRpc");
			base.__registerRpc(1109110496U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_1109110496), "attachPlayerClientRpc");
			base.__registerRpc(2790292547U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_2790292547), "attachPlayerSpikeClientRpc");
			base.__registerRpc(1314365859U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_1314365859), "SetColliderClientRpc");
			base.__registerRpc(1531827138U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_1531827138), "moaiSoundPlayClientRpc");
			base.__registerRpc(2582106502U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_2582106502), "setAnimationSpeedClientRpc");
			base.__registerRpc(1163343604U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_1163343604), "DoAnimationClientRpc");
			base.__registerRpc(3587374905U, new NetworkBehaviour.RpcReceiveHandler(ShamblerEnemy.__rpc_handler_3587374905), "animPlayClientRpc");
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2925436343(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).SetCapturedPlayerClientRpc(num, flag);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_3110006365(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).SetStabbedPlayerClientRpc(num, flag);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_3897930302(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).DmgPlayerClientRpc(num, num2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_2556090093(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			Vector3 vector;
			reader.ReadValueSafe(out vector);
			Vector3 vector2;
			reader.ReadValueSafe(out vector2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).DoGroundHopClientRpc(vector, vector2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_2229496956(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).deadEventClientRpc();
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_2192772121(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).letGoOfPlayerServerRpc(num);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_160126077(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			ulong num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).letGoOfPlayerClientRpc(num);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_836769684(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			int num2;
			ByteUnpacker.ReadValueBitPacked(reader, out num2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).attachPlayerServerRpc(num, flag, num2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_1109110496(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			int num2;
			ByteUnpacker.ReadValueBitPacked(reader, out num2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).attachPlayerClientRpc(num, flag, num2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_2790292547(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			int num2;
			ByteUnpacker.ReadValueBitPacked(reader, out num2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).attachPlayerSpikeClientRpc(num, flag, num2);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_1314365859(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).SetColliderClientRpc(num, flag);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_1531827138(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).moaiSoundPlayClientRpc(text);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_2582106502(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			float num;
			reader.ReadValueSafe<float>(out num, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).setAnimationSpeedClientRpc(num);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_1163343604(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if (networkManager == null || !networkManager.IsListening)
			{
				return;
			}
			int num;
			ByteUnpacker.ReadValueBitPacked(reader, out num);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).DoAnimationClientRpc(num);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		private static void __rpc_handler_3587374905(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Execute;
			((ShamblerEnemy)target).animPlayClientRpc(text);
			target.__rpc_exec_stage = NetworkBehaviour.__RpcExecStage.Send;
		}

		protected internal override string __getTypeName()
		{
			return "ShamblerEnemy";
		}

		protected Animator animator;

		private Vector3 nestSpot = Vector3.zero;

		public static List<ulong> stuckPlayerIds = new List<ulong>();

		protected float baseLeapChance = 100f;

		protected float leaptime = 0f;

		protected Vector3 leapPos;

		protected EntranceTeleport nearestEntrance = null;

		public Vector3 nearestEntranceNavPosition = Vector3.zero;

		protected PlayerControllerB mostRecentPlayer = null;

		protected int entranceDelay = 0;

		protected float chanceToLocateEntrancePlayerBonus = 0f;

		protected float chanceToLocateEntrance = 0f;

		public List<EnemyAI> unreachableEnemies = new List<EnemyAI>();

		public Vector3 itemNavmeshPosition = Vector3.zero;

		protected int sourcecycle = 75;

		protected float stamina = 0f;

		protected bool recovering = false;

		public int provokePoints = 0;

		public Transform turnCompass;

		protected float timeSinceHittingLocalPlayer;

		protected float timeSinceNewRandPos;

		protected Vector3 positionRandomness;

		protected Vector3 StalkPos;

		protected Random enemyRandom;

		protected bool isDeadAnimationDone;

		private bool markDead = false;

		public AudioSource creatureLeapLand;

		public AudioSource creatureAnger;

		private float angerSoundTimer = 0f;

		public AudioSource creatureLaugh;

		public AudioSource creaturePlant;

		public AudioSource creatureSneakyStab;

		public AudioSource creatureDeath;

		public AudioSource creatureTakeDmg;

		public AudioSource[] creatureSteps;

		protected float runAnimationCoefficient = 14f;

		protected float walkAnimationCoefficient = 3f;

		[Header("AI Debug")]
		public bool debugThoughts = true;

		public bool debugDrawGoal = true;

		private Vector3 lastGoal = Vector3.zero;

		private float lastGoalScore = 0f;

		private bool usingCustomGoal = false;

		private float sizeCheckCooldown = 2f;

		private bool stepSoundCycle1 = false;

		private bool stepSoundCycle2 = false;

		public float stabTimeout = 6f;

		private bool doneStab = false;

		private bool isStabbing = false;

		public Transform stabPoint;

		public AudioSource creatureStab;

		private bool doneLeap = false;

		private bool isLeaping = false;

		private float leapTimer = 9f;

		private float leapAnimationLength = 0.5f;

		private float leapPeakHeight = 3f;

		public List<ulong> EscapingEmployees = new List<ulong>();

		public PlayerControllerB capturedPlayer = null;

		public PlayerControllerB stabbedPlayer = null;

		private bool stabbedCapturedPlayer = false;

		public Transform capturePoint;

		public float captureRange = 5.35f;

		public float timeTillStab = 0f;

		private float crouchTimer = 0f;

		private float maxLeapDistance = 25f;

		public float crouchTimeout = 6f;

		private float spottedCooldown = 0f;

		private LayerMask WorldMask;

		private Vector3 stickyGoal = Vector3.zero;

		private float stickyScore = float.NegativeInfinity;

		private float stickyPickedAt = -999f;

		private float lastGoalDist = 9999f;

		private float stuckTimer = 0f;

		[SerializeField]
		private float goalMinLockSeconds = 1.2f;

		[SerializeField]
		private float goalRepathCooldown = 0.6f;

		[SerializeField]
		private float goalBetterBy = 0.35f;

		[SerializeField]
		private float goalArrivalTol = 0.9f;

		[SerializeField]
		private float stuckSpeedThresh = 0.15f;

		[SerializeField]
		private float stuckSeconds = 1.1f;

		private float coverLockUntil = -999f;

		private float lastSeenTime = -999f;

		[SerializeField]
		private float coverDwellSeconds = 5f;

		[SerializeField]
		private float seenGrace = 0.9f;

		public Transform footLCol;

		public Transform footRCol;

		public Transform armLCol;

		public Transform armRCol;

		public Transform headCol;

		[SerializeField]
		private float coverRequiredFraction = 0.7f;

		private float maxStabDistance = 4f;

		private float alertLevel = 0f;

		private float alertDecay = 1.5f;

		private float sneakyAlertLevel = 70f;

		private float alertRate = 1f;

		private float headingToNestTimeout = 6f;

		private float headingToNestDistance = 0f;

		private bool isPlanting = false;

		private bool donePlant = false;

		public GameObject heldStakeRef;

		public float plantCooldown = 5f;

		public float plantTime = -1f;

		private PlayerControllerB RageTarget = null;

		public enum State
		{
			SearchingForPlayer,
			HeadingToEntrance,
			Crouching,
			Leaping,
			ClosingDistance,
			StabbingCapturedPlayer,
			HeadingToNest,
			PlantingStake,
			SneakyStab
		}
	}
}
