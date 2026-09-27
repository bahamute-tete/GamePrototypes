using UnityEngine;

namespace BlockShooting.ParkourPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public class ParkourRunner : MonoBehaviour
    {
        public Transform viewCamera;
        public Vector3[] checkpoints;
        public Vector3 finish;
        CharacterController motor;
        Vector3 spawn;
        float vertical, started, finishedTime, coyote, jumpBuffer;
        int checkpoint, falls;
        bool complete;
        GUIStyle heading, body, small;

        void Start()
        {
            motor = GetComponent<CharacterController>();
            spawn = transform.position;
            started = Time.time;
            if (viewCamera) viewCamera.position = transform.position + new Vector3(0, 7, -10);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) { checkpoint = 0; spawn = checkpoints[0]; complete = false; falls = 0; started = Time.time; Respawn(); }
            if (complete) return;
            if (motor.isGrounded) { coyote = .12f; if (vertical < 0) vertical = -2; } else coyote -= Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Space)) jumpBuffer = .12f; else jumpBuffer -= Time.deltaTime;
            if (jumpBuffer > 0 && coyote > 0) { vertical = 11.5f; coyote = 0; jumpBuffer = 0; }
            Vector3 move = Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")), 1);
            vertical -= 25 * Time.deltaTime;
            motor.Move((move * (Input.GetKey(KeyCode.LeftShift) ? 11 : 8) + Vector3.up * vertical) * Time.deltaTime);
            if (transform.position.y < -8) { falls++; Respawn(); }
            for (int i = checkpoint + 1; i < checkpoints.Length; i++)
                if (Vector3.Distance(transform.position, checkpoints[i]) < 2.8f && motor.isGrounded) { checkpoint = i; spawn = checkpoints[i]; }
            if (Vector3.Distance(transform.position, finish) < 3 && motor.isGrounded) { complete = true; finishedTime = Time.time - started; }
        }

        void LateUpdate()
        {
            if (!viewCamera) return;
            viewCamera.position = Vector3.Lerp(viewCamera.position, transform.position + new Vector3(0, 7, -10), 1 - Mathf.Exp(-7 * Time.deltaTime));
            viewCamera.LookAt(transform.position + new Vector3(0, 1, 4));
        }

        void Respawn()
        {
            motor.enabled = false;
            transform.position = spawn;
            vertical = 0;
            motor.enabled = true;
        }

        void OnGUI()
        {
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold }; heading.normal.textColor = Color.white;
                body = new GUIStyle(GUI.skin.label) { fontSize = 16 }; body.normal.textColor = new Color(.7f, .95f, 1);
                small = new GUIStyle(body) { fontSize = 13 }; small.normal.textColor = new Color(.7f, .78f, .85f);
            }
            GUI.color = new Color(.025f, .06f, .1f, .93f); GUI.DrawTexture(new Rect(22, 22, 360, 158), Texture2D.whiteTexture);
            GUI.color = new Color(.05f, .88f, .85f); GUI.DrawTexture(new Rect(22, 22, 4, 158), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(42, 33, 320, 36), "SKYLINE / PARKOUR", heading);
            GUI.Label(new Rect(42, 75, 330, 26), $"TIME  {(complete ? finishedTime : Time.time - started):000.0}   /   FALLS  {falls:00}", body);
            GUI.Label(new Rect(42, 104, 330, 25), $"CHECKPOINT  {checkpoint + 1} / {checkpoints.Length}", body);
            GUI.Label(new Rect(42, 144, 330, 25), "WASD move   SPACE jump   SHIFT sprint   R restart", small);
            if (complete)
            {
                GUI.color = new Color(.025f, .06f, .1f, .95f); GUI.DrawTexture(new Rect(Screen.width / 2 - 220, Screen.height / 2 - 60, 440, 120), Texture2D.whiteTexture); GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 40, 400, 40), "COURSE COMPLETE", heading);
                GUI.Label(new Rect(Screen.width / 2 - 180, Screen.height / 2 + 5, 400, 30), $"{finishedTime:0.0} seconds  /  Press R to run again", body);
            }
        }
    }
}
