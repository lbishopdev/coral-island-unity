using UnityEngine;
using UnityEngine.InputSystem;

namespace TideAndTill
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        private GameState state;
        private GameInput input;
        private FarmSystem farm;
        private CharacterController controller;
        private Transform avatar;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftLeg;
        private Transform rightLeg;
        private WorldInteractable nearbyInteractable;
        private float verticalVelocity;
        private float stride;
        private float toolSwing;
        private Vector3 lastSafePosition;
        private float airborneTime;

        /// <summary>
        /// Longest the character controller will tolerate without a ground
        /// contact before grounding recovery kicks in. If the terrain collider
        /// ever stops reporting a surface under the player, this snaps the
        /// player back down instead of letting them fall out of the world.
        /// </summary>
        private const float MaxAirborneTime = 1.5f;

        /// <summary>
        /// How far below the analytic terrain height the player may sit before
        /// grounding recovery treats them as buried in the ground.
        /// </summary>
        private const float SunkenTolerance = 0.35f;

        public string InteractionPrompt => nearbyInteractable == null ? string.Empty : nearbyInteractable.Prompt;

        public static PlayerController Create(Transform parent, GameState state, GameInput input, FarmSystem farm)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(-1.6f, WorldBuilder.SampleHeight(-1.6f, -4.5f) + 0.12f, -4.5f);
            var character = player.AddComponent<CharacterController>();
            character.height = 1.78f;
            character.radius = 0.34f;
            character.center = new Vector3(0f, 0.89f, 0f);
            character.stepOffset = 0.38f;
            character.slopeLimit = 48f;
            character.skinWidth = 0.04f;

            var controller = player.AddComponent<PlayerController>();
            controller.Initialize(state, input, farm);
            return controller;
        }

        private void Initialize(GameState gameState, GameInput gameInput, FarmSystem farmSystem)
        {
            state = gameState;
            input = gameInput;
            farm = farmSystem;
            controller = GetComponent<CharacterController>();
            lastSafePosition = transform.position;
            BuildAvatar();
        }

        private void BuildAvatar()
        {
            avatar = new GameObject("Farmer Avatar").transform;
            avatar.SetParent(transform, false);

            // Body and head use rounded primitives for an approachable, readable silhouette.
            PrimitiveFactory.Capsule("Torso", avatar, new Vector3(0f, 1.13f, 0f), new Vector3(0.42f, 0.46f, 0.34f), IslandMaterials.TealWood, false);
            PrimitiveFactory.Sphere("Head", avatar, new Vector3(0f, 1.83f, 0f), new Vector3(0.43f, 0.48f, 0.42f), IslandMaterials.Skin, false);
            PrimitiveFactory.Sphere("Hair", avatar, new Vector3(0f, 2.02f, 0.07f), new Vector3(0.46f, 0.29f, 0.45f), IslandMaterials.DarkWood, false);
            PrimitiveFactory.Cube("Shorts", avatar, new Vector3(0f, 0.76f, 0f), new Vector3(0.62f, 0.36f, 0.48f), IslandMaterials.DarkRoof, false);

            // Wide straw hat is recognizable at the elevated camera angle.
            PrimitiveFactory.Cylinder("Hat Brim", avatar, new Vector3(0f, 2.24f, 0f), new Vector3(0.57f, 0.035f, 0.57f), IslandMaterials.Hay, false);
            PrimitiveFactory.Cylinder("Hat Crown", avatar, new Vector3(0f, 2.39f, 0f), new Vector3(0.34f, 0.15f, 0.34f), IslandMaterials.Hay, false);
            PrimitiveFactory.Cylinder("Hat Band", avatar, new Vector3(0f, 2.27f, 0f), new Vector3(0.355f, 0.035f, 0.355f), IslandMaterials.Coral, false);

            leftArm = CreateLimb("Left Arm", new Vector3(-0.46f, 1.28f, 0f), IslandMaterials.Skin, true);
            rightArm = CreateLimb("Right Arm", new Vector3(0.46f, 1.28f, 0f), IslandMaterials.Skin, true);
            leftLeg = CreateLimb("Left Leg", new Vector3(-0.20f, 0.45f, 0f), IslandMaterials.DarkRoof, false);
            rightLeg = CreateLimb("Right Leg", new Vector3(0.20f, 0.45f, 0f), IslandMaterials.DarkRoof, false);
            PrimitiveFactory.Sphere("Left Boot", leftLeg, new Vector3(0f, -0.32f, 0.08f), new Vector3(0.17f, 0.12f, 0.25f), IslandMaterials.DarkWood, false);
            PrimitiveFactory.Sphere("Right Boot", rightLeg, new Vector3(0f, -0.32f, 0.08f), new Vector3(0.17f, 0.12f, 0.25f), IslandMaterials.DarkWood, false);
        }

        private Transform CreateLimb(string name, Vector3 localPosition, Material material, bool arm)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(avatar, false);
            pivot.localPosition = localPosition;
            PrimitiveFactory.Capsule(name + " Mesh", pivot, new Vector3(0f, arm ? -0.24f : -0.22f, 0f),
                new Vector3(arm ? 0.16f : 0.19f, arm ? 0.29f : 0.31f, arm ? 0.16f : 0.19f), material, false);
            return pivot;
        }

        private void Update()
        {
            ReadToolSelection();
            Move();
            UpdateTarget();
            UseActions();
            AnimateAvatar();
        }

        private void ReadToolSelection()
        {
            int hotkey = input.ReadToolHotkey();
            if (hotkey >= 0)
                state.SelectTool((FarmTool)hotkey);
            int cycle = input.ReadToolCycle();
            if (cycle != 0)
                state.CycleTool(cycle);
        }

        private void Move()
        {
            Vector2 moveInput = input.Move;
            Camera camera = Camera.main;
            Vector3 forward = camera == null ? Vector3.forward : camera.transform.forward;
            Vector3 right = camera == null ? Vector3.right : camera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 movement = forward * moveInput.y + right * moveInput.x;
            if (movement.sqrMagnitude > 1f) movement.Normalize();
            float speed = input.SprintHeld && state.Stamina > 0.5f ? 5.5f : 3.75f;
            if (input.SprintHeld && movement.sqrMagnitude > 0.1f)
                state.SpendStamina(Time.deltaTime * 1.8f);
            else if (movement.sqrMagnitude < 0.02f)
                state.RegenerateStamina(Time.deltaTime * 1.2f);

            if (controller.isGrounded)
            {
                airborneTime = 0f;
                if (verticalVelocity < 0f) verticalVelocity = -2.4f;
            }
            else
            {
                airborneTime += Time.deltaTime;
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            controller.Move((movement * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
            RecoverGrounding();

            if (movement.sqrMagnitude > 0.025f)
            {
                Quaternion facing = Quaternion.LookRotation(movement, Vector3.up);
                avatar.rotation = Quaternion.Slerp(avatar.rotation, facing, 13f * Time.deltaTime);
                stride += Time.deltaTime * speed * 3.1f;
            }
            else
            {
                stride = Mathf.Lerp(stride, Mathf.Round(stride / Mathf.PI) * Mathf.PI, Time.deltaTime * 4f);
            }
        }

        /// <summary>
        /// Keeps the character controller attached to the island.
        /// Leaving the playable bounds or dropping under the world is a hard
        /// failure, so the player returns to the last known-good position.
        /// Sinking below the terrain surface, or failing to find any surface for
        /// longer than <see cref="MaxAirborneTime"/>, recovers locally by
        /// standing the player back on the terrain column they are over. Without
        /// this second path a one-sided or missing ground collider leaves the
        /// player falling forever instead of recovering.
        /// </summary>
        private void RecoverGrounding()
        {
            float nx = transform.position.x / 42f;
            float nz = transform.position.z / 32f;
            bool strayed = nx * nx + nz * nz > 0.94f;
            bool fellThrough = transform.position.y < -2.5f;

            if (strayed || fellThrough)
            {
                RecoverTo(lastSafePosition);
                return;
            }

            float ground = WorldBuilder.SampleHeight(transform.position.x, transform.position.z);
            bool recoverable = ground > -0.4f;
            // The mesh is a discretisation of SampleHeight, so allow a little
            // slack before treating the player as buried in the terrain.
            bool sunken = transform.position.y < ground - SunkenTolerance;
            bool lostGround = airborneTime > MaxAirborneTime;

            if (recoverable && (sunken || lostGround))
            {
                RecoverTo(new Vector3(transform.position.x, ground + 0.15f, transform.position.z));
            }
            else if (controller.isGrounded)
            {
                lastSafePosition = transform.position;
            }
        }

        private void RecoverTo(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalVelocity = 0f;
            airborneTime = 0f;
            // Remember where recovery landed so the guard can never loop on a
            // stale position that is itself unreachable.
            lastSafePosition = position;
        }

        private void UpdateTarget()
        {
            Vector3 facing = avatar.forward;
            facing.y = 0f;
            farm.SelectNearest(transform.position + facing.normalized * 1.25f);
            nearbyInteractable = WorldInteractable.FindNearest(transform.position, 2.6f);
        }

        private void UseActions()
        {
            if (input.InteractPressed && nearbyInteractable != null)
                nearbyInteractable.Interact(this);

            if (!input.UsePressed) return;

            bool acted;
            if (state.SelectedTool == FarmTool.Axe)
            {
                ChoppableInteractable stump = WorldInteractable.FindNearest<ChoppableInteractable>(transform.position, 2.5f);
                acted = stump != null && stump.Chop(state);
                if (!acted && stump == null)
                    state.ShowToast("Move close to an old stump to swing the axe.");
            }
            else
            {
                acted = farm.UseSelected(state.SelectedTool);
            }

            if (acted)
                toolSwing = 1f;
        }

        private void AnimateAvatar()
        {
            float moving = input.Move.sqrMagnitude > 0.03f ? 1f : 0f;
            float swing = Mathf.Sin(stride) * 28f * moving;
            leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            leftArm.localRotation = Quaternion.Euler(-swing * 0.72f, 0f, 5f);

            if (toolSwing > 0f)
            {
                toolSwing = Mathf.Max(0f, toolSwing - Time.deltaTime * 2.8f);
                float arc = Mathf.Sin((1f - toolSwing) * Mathf.PI) * 105f;
                rightArm.localRotation = Quaternion.Euler(-25f - arc, 0f, -8f);
            }
            else
            {
                rightArm.localRotation = Quaternion.Euler(swing * 0.72f, 0f, -5f);
            }
            avatar.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(stride)) * 0.025f * moving, 0f);
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            lastSafePosition = position;
            verticalVelocity = 0f;
            airborneTime = 0f;
        }
    }

    [RequireComponent(typeof(Camera))]
    public sealed class IslandCamera : MonoBehaviour
    {
        private Transform target;
        private GameInput input;
        private float yaw = 42f;
        private float pitch = 39f;
        private float distance = 12.5f;
        private Vector3 smoothVelocity;

        public void Initialize(Transform follow, GameInput gameInput)
        {
            target = follow;
            input = gameInput;
            Snap();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector2 gamepadLook = input.Look;
            yaw += gamepadLook.x * 90f * Time.deltaTime;
            pitch = Mathf.Clamp(pitch - gamepadLook.y * 55f * Time.deltaTime, 25f, 57f);

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.16f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, 25f, 57f);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.equalsKey.isPressed) distance -= Time.deltaTime * 6f;
                if (keyboard.minusKey.isPressed) distance += Time.deltaTime * 6f;
                distance = Mathf.Clamp(distance, 8.5f, 17f);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * 1.1f;
            Vector3 wanted = focus - rotation * Vector3.forward * distance;
            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref smoothVelocity, 0.12f);
            transform.rotation = rotation;
        }

        private void Snap()
        {
            if (target == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * 1.1f;
            transform.position = focus - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
        }
    }
}
