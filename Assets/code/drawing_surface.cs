using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class drawing_surface : MonoBehaviour, IPlayerInteractable, INonEquipable, INonBlueprintable, IExtendsNetworked
{
    public int xsize = 10;
    public int ysize = 10;
    public float pixel_size = 0.1f;
    public Material material;

    GameObject drawing_quad;

    Texture2D tex
    {
        get
        {
            if (_tex == null)
            {
                _tex = new Texture2D(xsize, ysize);
                _tex.filterMode = FilterMode.Point;

                for (int x = 0; x < xsize; ++x)
                    for (int y = 0; y < ysize; ++y)
                        _tex.SetPixel(x, y, new Color(1, 1, 1, 0));

                _tex.Apply();
            }
            return _tex;
        }
    }
    Texture2D _tex;

    private void Start()
    {
        var bc = gameObject.AddComponent<BoxCollider>();
        bc.size = new Vector3(xsize * pixel_size, ysize * pixel_size, 0.001f);

        drawing_quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        drawing_quad.transform.SetParent(transform);
        drawing_quad.transform.localPosition = Vector3.zero;
        drawing_quad.transform.localRotation = Quaternion.identity;
        drawing_quad.transform.localScale = new Vector3(xsize * pixel_size, ysize * pixel_size);

        var mr = drawing_quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.material.mainTexture = tex;
    }

    void set_pixel(int x, int y, Color col, bool save = true)
    {
        if (x < 0 || y < 0 || x >= xsize || y >= ysize) return;
        if (save) texture_data[x, y] = col;
        tex.SetPixel(x, y, col);
        tex.Apply();
    }

    void get_coords(Vector3 point, out int x, out int y)
    {
        Vector3 bottom_left = transform.position -
            transform.right * xsize * pixel_size / 2f -
            transform.up * ysize * pixel_size / 2f;

        Vector3 delta = point - bottom_left;

        x = (int)(Vector3.Dot(delta, transform.right) / pixel_size);
        y = (int)(Vector3.Dot(delta, transform.up) / pixel_size);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(xsize * pixel_size, ysize * pixel_size, 0.0001f));
    }

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[] { new drawing_interaction(this) };
    }

    class drawing_interaction : player_interaction
    {
        drawing_surface surface;
        Color color = Color.black;
        RectTransform ui;

        public drawing_interaction(drawing_surface surface) => this.surface = surface;
        public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
        public override string context_tip() => "draw on surface";
        public override bool allows_mouse_look() => false;
        public override bool allows_movement() => false;

        public override bool start_interaction(player player)
        {
            ui = Resources.Load<RectTransform>("ui/drawing_surface").inst();
            ui.transform.SetParent(game.canvas.transform);
            ui.transform.localPosition = Vector3.zero;

            foreach (var b in ui.GetComponentsInChildren<UnityEngine.UI.Button>())
                b.onClick.AddListener(() =>
                {
                    color = b.image.color;
                });

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            player.current.cursor_sprite = null;

            return false;
        }

        public override bool continue_interaction(player player)
        {
            // Draw with left click
            if (controls.held(controls.BIND.USE_ITEM) || controls.held(controls.BIND.ALT_USE_ITEM))
            {
                // Can draw on any surface
                var ds = utils.raycast_for_closest<drawing_surface>(
                    player.current.camera.ScreenPointToRay(Input.mousePosition),
                    out RaycastHit hit, max_distance: player.INTERACTION_RANGE);

                if (ds != null)
                {
                    ds.get_coords(hit.point, out int x, out int y);
                    ds.set_pixel(x, y, color);

                    // Erase with right click
                    if (controls.held(controls.BIND.ALT_USE_ITEM))
                        ds.set_pixel(x, y, new Color(1, 1, 1, 0));
                }
            }

            // Stop interaction by pressing E
            if (controls.triggered(controls.BIND.OPEN_INVENTORY))
                Destroy(ui.gameObject);

            return ui == null; // Interaction complete when ui deleted
        }

        public override void end_interaction(player player)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            player.current.cursor_sprite = cursors.DEFAULT;
            player.current.validate_equip();
        }
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.networked_texture texture_data;

    public IExtendsNetworked.callbacks get_callbacks()
    {
        return new IExtendsNetworked.callbacks
        {
            init_networked_variables = () =>
            {
                texture_data = new networked_variables.networked_texture(xsize, ysize);
                texture_data.on_deserialize += () =>
                {
                    for (int x = 0; x < xsize; ++x)
                        for (int y = 0; y < ysize; ++y)
                            set_pixel(x, y, texture_data[x, y], save: false);
                };
            }
        };
    }
}
