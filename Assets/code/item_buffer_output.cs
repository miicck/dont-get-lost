using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_buffer_output : item_output
{
    public bool ready_for_input
    {
        get
        {
            // Only ready if the output is 100% free
            if (dropping != null) return false;
            var no = peek_next_output();
            if (no == null) return false;
            if (no.item_count > 0) return false;
            return true;
        }
    }

    item_dropper dropping;

    private void Update()
    {
        if (item_count == 0) return; // No items => nothing to do
        dropping = item_dropper.create(release_next_item(), transform.position, next_output());
    }
}
