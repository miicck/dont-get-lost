using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class networked_monobehaviour_test : MonoBehaviour
{
    class section : network_section
    {
        int x;

        // Set the x and z coordinates
        protected override void on_create(object[] creation_args)
        {
            x = (int)creation_args[0];
            name = "section " + x;
        }

        // A section is compared by it's x coordinate
        protected override byte[] comparison_bytes()
        {
            return System.BitConverter.GetBytes(x);
        }
    }

    class subsection : networked_monobehaviour
    {

    }

    public void start_server()
    {
        server.start(6969);
    }

    public void start_client()
    {
        networked_monobehaviour.connect_to_server(server.local_ip_address().ToString(), 6969);
    }

    Dictionary<int, section> sections = new Dictionary<int, section>();
    public void load_section(int x)
    {
        sections[x] = network_section.create<section>(x);
    }

    public void add_subsection(int section_x)
    {
        networked_monobehaviour.create<subsection>(sections[section_x]);
    }

    private void Update()
    {
        server.update();
        networked_monobehaviour.update();
    }
}