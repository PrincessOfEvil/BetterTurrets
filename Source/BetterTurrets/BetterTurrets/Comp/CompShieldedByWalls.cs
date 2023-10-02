using System.Linq;
using Verse;

namespace BetterTurrets;

public class CompShieldedByWalls : ThingComp
	{
	private Thing shield;
	
	public override void PostPreApplyDamage(DamageInfo dinfo, out bool absorbed)
		{
		base.PostPreApplyDamage(dinfo, out absorbed);

		if (parent.holdingOwner != null) return;
		if (shield == null || shield.Destroyed)
			{
			var list = parent.Position.GetThingList(parent.Map)
						 .Where(t => t.def.category == ThingCategory.Building && t.def.useHitPoints &&
									 t.def.passability >= Traversability.PassThroughOnly && t != parent)
						 .OrderBy(t => t.MaxHitPoints);
			if (list.EnumerableNullOrEmpty()) return;
			shield = list.First();
			}
		shield.TakeDamage(dinfo);
		absorbed = true;
		}
	}