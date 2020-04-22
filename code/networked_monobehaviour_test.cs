using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class networked_monobehaviour_test : MonoBehaviour
{
    class section : networked.section
    {
        int x;

        // Set the x and z coordinates
        protected override void on_create(object[] creation_args)
        {
            x = (int)creation_args[0];
            name = "section " + x;
        }

        // A section is identified by it's x coordinate
        public override byte[] section_id_bytes()
        {
            return System.BitConverter.GetBytes(x);
        }
    }

    class subsection : networked
    {

    }

    public void start_server()
    {
        networked.server.start(6969);
    }

    public void start_client()
    {
        networked.client.connect_to_server(networked.server.local_ip_address().ToString(), 6969);
    }

    Dictionary<int, section> sections = new Dictionary<int, section>();
    public void load_section(int x)
    {
        sections[x] = networked.section.create<section>(x);
    }

    public void add_subsection(int section_x)
    {
        networked.create<subsection>(sections[section_x]);
    }

    private void Update()
    {
        networked.server.update();
        networked.client.update();
    }
}