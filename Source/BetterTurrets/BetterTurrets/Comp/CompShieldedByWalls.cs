using System.Linq;
using Verse;

namespace BetterTurrets;

public class CompShieldedByWalls : ThingComp
	{
	private Thing shield;
	
	public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
		base.PostPreApplyDamage(ref dinfo, out absorbed);

		if (parent.holdingOwner != null) return;
		if (shield == null || shield.Destroyed)
			{
			Thing sThing = parent.Position.GetThingList(parent.Map)
			                     .Where(t => t.def.category == ThingCategory.Building && t.def.useHitPoints &&
			                                 t.def.passability >= Traversability.PassThroughOnly && t != parent)
			                     .OrderBy(t => t.MaxHitPoints).FirstOrFallback();
			if (sThing is null) return;
			shield = sThing;
			}
		shield.TakeDamage(dinfo);
		absorbed = true;
		}
	}