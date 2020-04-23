using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class networked_monobehaviour_test : MonoBehaviour
{
    class section : networked.section
    {
        int x;
        new Renderer renderer;

        // Set the x and z coordinates
        protected override void on_create()
        {
            name = "section " + x;

            transform.position = new Vector3(2 * x - 10, 0, 0);

            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.transform.SetParent(transform);
            q.transform.localPosition = new Vector3(0, 0, 0.1f);
            q.transform.localScale = new Vector3(1, 10, 1);
            renderer = q.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = Color.white;
        }

        public override void section_id_initialize(params object[] section_id_init_args)
        {
            x = (int)section_id_init_args[0];
        }

        // A section is identified by it's x coordinate
        public override byte[] section_id_bytes()
        {
            return System.BitConverter.GetBytes(x);
        }
    }

    class subsection : networked
    {
        new Renderer renderer;

        protected override void on_create()
        {
            transform.localPosition = new Vector3(0, 5 - transform.parent.childCount, 0);
            name = "subsection " + transform.parent.childCount;

            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.transform.SetParent(transform);
            q.transform.localPosition = Vector3.zero;

            renderer = q.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = random_color();
        }

        protected override byte[] serialize()
        {

            // Serialize the color of this subsection
            return concat_buffers(
                System.BitConverter.GetBytes(renderer.material.color.r),
                System.BitConverter.GetBytes(renderer.material.color.g),
                System.BitConverter.GetBytes(renderer.material.color.b)
            );
        }

        protected override void deserialize(byte[] bytes, int offset, int count)
        {
            // Deserialize the color of this subsection
            renderer.material.color = new Color(
                System.BitConverter.ToSingle(bytes, offset),
                System.BitConverter.ToSingle(bytes, offset + sizeof(float)),
                System.BitConverter.ToSingle(bytes, offset + sizeof(float) * 2),
                renderer.material.color.a
            );
        }

        Color random_color()
        {
            return new Color(
                Random.Range(0, 1f),
                Random.Range(0, 1f),
                Random.Range(0, 1f)
            );
        }

        public void randomize_color()
        {
            renderer.material.color = random_color();
        }
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

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            var ss = utils.raycast_for_closest<subsection>(
                Camera.main.ScreenPointToRay(Input.mousePosition), out hit);
            if (ss != null) ss.randomize_color();
        }
    }
}