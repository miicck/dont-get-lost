using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class railway : building_material
{
    static HashSet<railway> railways = new HashSet<railway>();
    railway_snap_point[] rail_snap_points => GetComponentsInChildren<railway_snap_point>();

    public railway next
    {
        get
        {
            foreach (var r in rail_snap_points)
                if (r.outward && r.linked_to?.railway != null)
                    return r.linked_to.railway;
            return null;
        }
    }

    public override void on_create()
    {
        base.on_create();
        validate_links();
    }

    public void validate_links()
    {
        foreach (var sp_this in rail_snap_points)
            foreach (var r in railways)
            {
                foreach (var sp_other in r.rail_snap_points)
                    if (sp_this.try_link(sp_other))
                        break;
                if (sp_this.linked_to != null) break;
            }

        railways.Add(this);
    }

    private void OnDestroy()
    {
        foreach (var r in rail_snap_points)
            r.unlink();
        railways.Remove(this);
    }

    private void OnDrawGizmos()
    {
        if (next == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, next.transform.position);
    }

    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        if (use_type == player.USE_TYPE.USING_RIGHT_CLICK)
            if (player.current.equipped == null)
            {


                return use_result.complete;
            }

        return base.on_use_start(use_type);
    }
}
