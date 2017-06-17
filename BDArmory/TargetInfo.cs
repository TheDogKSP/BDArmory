using System.Collections;
using System.Collections.Generic;
using BDArmory.Parts;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory
{
	public class TargetInfo : MonoBehaviour
	{
		public BDArmorySettings.BDATeams team;
		public bool isMissile;
		public MissileBase MissileBaseModule;
		public MissileFire weaponManager;
        List<MissileFire> friendliesEngaging;
        public float detectedTime;
        Coroutine lifeRoutine;
        Coroutine massRoutine;

        public bool isLanded
		{
			get
			{
				if(!vessel) return false;
				return vessel.LandedOrSplashed;
			}
		}
		public Vector3 velocity
		{
			get
			{
				if(!vessel) return Vector3.zero;
				return vessel.srf_velocity;
			}
		}
		public Vector3 position
        {
            get
            {
                return vessel.vesselTransform.position;
            }
        }

		private Vessel vessel;
		public Vessel Vessel
		{
			get
			{
				if(!vessel)
				{
					vessel = GetComponent<Vessel>();
				}

				return vessel;
			}
			set
			{
				vessel = value;
			}
		}

		public bool isThreat
		{
			get
			{
				if(!Vessel)
				{
					return false;
				}

				if(isMissile && MissileBaseModule && !MissileBaseModule.HasMissed)
				{
					return true;
				}
				else if(weaponManager && weaponManager.vessel.IsControllable)
				{
					return true;
				}

				return false;
			}
		}		
        
		void Awake()
		{
			if(!vessel)
			{
				vessel = GetComponent<Vessel>();
			}
			if(!vessel)
			{
				Debug.Log ("[BDArmory]:TargetInfo was added to a non-vessel");
				Destroy (this);
				return;
			}

			//destroy this if a target info is already attached to the vessel
			foreach(TargetInfo otherInfo in vessel.gameObject.GetComponents<TargetInfo>())
			{
				if(otherInfo != this)
				{
					Destroy(this);
					return;
				}
			}

			team = BDArmorySettings.BDATeams.None;

			bool foundMf = false;
			foreach(MissileFire mf in vessel.FindPartModulesImplementing<MissileFire>())
			{
				foundMf = true;
				team = BDATargetManager.BoolToTeam(mf.team);
				weaponManager = mf;
				break;
			}
			if(!foundMf)
			{
				foreach(MissileBase ml in vessel.FindPartModulesImplementing<MissileBase>())
				{
					isMissile = true;
					MissileBaseModule = ml;
					team = BDATargetManager.BoolToTeam(ml.Team);
					break;
				}
			}
				
			if(team != BDArmorySettings.BDATeams.None)
			{
				if(!BDATargetManager.TargetDatabase[BDATargetManager.OtherTeam(team)].Contains(this))
				{
					BDATargetManager.TargetDatabase[BDATargetManager.OtherTeam(team)].Add(this);
				}
			}

			friendliesEngaging = new List<MissileFire>();

			vessel.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
			lifeRoutine = StartCoroutine(LifetimeRoutine());
			//add delegate to peace enable event
			BDArmorySettings.OnPeaceEnabled += OnPeaceEnabled;

			if(!isMissile && team != BDArmorySettings.BDATeams.None)
			{
				massRoutine = StartCoroutine(MassRoutine());
			}
		}

		void OnPeaceEnabled()
		{
			if(lifeRoutine != null)
			{
				StopCoroutine(lifeRoutine);
			}
			if(massRoutine != null)
			{
				StopCoroutine(massRoutine);
			}

			Destroy(this);
		}

		void OnDestroy()
		{
			//remove delegate from peace enable event
			BDArmorySettings.OnPeaceEnabled -= OnPeaceEnabled;
		}
	
		IEnumerator LifetimeRoutine()
		{
			detectedTime = Time.time;
			while(Time.time - detectedTime < 60 && enabled)
			{
				yield return null;
			}
			if(massRoutine != null)
			{
				StopCoroutine(massRoutine);
			}
			Destroy(this);
		}
		
		IEnumerator MassRoutine()
		{
			float startMass = vessel.GetTotalMass();
			while(enabled)
			{
				if(vessel.GetTotalMass() < startMass / 4)
				{
					if(lifeRoutine != null)
					{
						StopCoroutine(lifeRoutine);
					}

					RemoveFromDatabases();
					yield break;
				}
				yield return new WaitForSeconds(1);
			}
		}

		void Update()
		{
			if(!vessel)
			{
				AboutToBeDestroyed();
			}
			else
			{
				if(vessel.vesselType == VesselType.Debris)
				{
					RemoveFromDatabases();
					team = BDArmorySettings.BDATeams.None;
				}
			}
		}

		public int numFriendliesEngaging
		{
			get
			{
				if(friendliesEngaging == null)
				{
					return 0;
				}
				friendliesEngaging.RemoveAll(item => item == null);
				return friendliesEngaging.Count;
			}
		}

		public void Engage(MissileFire mf)
		{
			if(!friendliesEngaging.Contains(mf))
			{
				friendliesEngaging.Add(mf);
			}
		}

		public void Disengage(MissileFire mf)
		{
			friendliesEngaging.Remove(mf);
		}
		
		void AboutToBeDestroyed()
		{
			RemoveFromDatabases();
			Destroy(this);
		}

		public bool IsCloser(TargetInfo otherTarget, MissileFire myMf)
		{
			float thisSqrDist = (position-myMf.transform.position).sqrMagnitude;
			float otherSqrDist = (otherTarget.position-myMf.transform.position).sqrMagnitude;
			return thisSqrDist < otherSqrDist;
		}

		public void RemoveFromDatabases()
		{
			BDATargetManager.TargetDatabase[BDArmorySettings.BDATeams.A].Remove(this);
			BDATargetManager.TargetDatabase[BDArmorySettings.BDATeams.B].Remove(this);
		}
	}
}

