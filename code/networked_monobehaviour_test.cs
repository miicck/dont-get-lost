using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class networked_monobehaviour_test : MonoBehaviour
{
    public UnityEngine.UI.Text server_info;
    public UnityEngine.UI.Text client_info;
    public float network_fps = 60f;

    class section : networked.section
    {
        int x;
        new Renderer renderer;

        // Set the x and z coordinates
        public override void on_create()
        {
            name = "section " + x;

            transform.position = new Vector3(2 * x - 9.5f, 0, 0);

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

        public override void invert_id(byte[] id_bytes)
        {
            x = System.BitConverter.ToInt32(id_bytes, 0);
        }
    }

    class subsection : networked
    {
        new Renderer renderer;

        protected override void on_create(bool local)
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
            // Serialize the position / color of this subsection
            return concat_buffers(
                System.BitConverter.GetBytes(transform.position.x),
                System.BitConverter.GetBytes(transform.position.y),
                System.BitConverter.GetBytes(transform.position.z),
                System.BitConverter.GetBytes(renderer.material.color.r),
                System.BitConverter.GetBytes(renderer.material.color.g),
                System.BitConverter.GetBytes(renderer.material.color.b)
            );
        }

        protected override void deserialize(byte[] bytes, int offset, int count)
        {
            // Deserialize the position of this subsection
            transform.position = new Vector3(
                System.BitConverter.ToSingle(bytes, offset + sizeof(float) * 0),
                System.BitConverter.ToSingle(bytes, offset + sizeof(float) * 1),
                System.BitConverter.ToSingle(bytes, offset + sizeof(float) * 2)
            );

            // Deserialize the color of this subsection
            renderer.material.color = new Color(
                System.BitConverter.ToSingle(bytes, offset + sizeof(float) * 3),
                System.BitConverter.ToSingle(bytes, offset + sizeof(float) * 4),
                System.BitConverter.ToSingle(bytes, offset + sizeof(float) * 5),
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

    private void Start()
    {
        InvokeRepeating("network_update", 0f, 1f / network_fps);
    }

    public void start_server()
    {
        networked.server.start(networked.server.DEFAULT_PORT, "test");
    }

    public void start_client()
    {
        networked.client.connect_to_server(networked.server.local_ip_address().ToString(), networked.server.DEFAULT_PORT);
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
        server_info.text = networked.server.info();
        client_info.text = networked.client.info();

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            var ss = utils.raycast_for_closest<subsection>(
                Camera.main.ScreenPointToRay(Input.mousePosition), out hit);
            if (ss != null) ss.randomize_color();
        }

        float fb = 0f;
        if (Input.GetKey(KeyCode.W)) fb = 1f;
        if (Input.GetKey(KeyCode.S)) fb = -1f;

        if (fb != 0)
        {
            RaycastHit hit;
            var ss = utils.raycast_for_closest<subsection>(
                Camera.main.ScreenPointToRay(Input.mousePosition), out hit);
            if (ss != null)
            {
                ss.transform.position += Vector3.up * Time.deltaTime * fb / 5f;
            }
        }
    }

    void network_update()
    {
        networked.server.update();
        networked.client.update();
    }

    private void OnApplicationQuit()
    {
        if (networked.client.connected) networked.client.disconnect();
    }
}