using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chair : MonoBehaviour, INotPathBlocking
{

    static chair()
    {
        help_book.add_entry("towns/Dining spots/Chair",
        () => "Chairs go in the inventory of dining tables." 
        );
    }

}
