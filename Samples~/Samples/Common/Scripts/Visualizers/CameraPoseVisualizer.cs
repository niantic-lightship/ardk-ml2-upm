using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    public class CameraPoseVisualizer :
        MonoBehaviour
    {
        private GameObject _parent;
        private LineRenderer CameraPoseRenderer;
        private Camera _camera;
        private Gradient _validGradient;
        private Gradient _inValidGradient;

        void Start()
        {
            // Only one line renderer can be added per GO, so spawn a new one
            _parent = new GameObject();
            _camera = Camera.main;
            CameraPoseRenderer = _parent.AddComponent<LineRenderer>();
            CameraPoseRenderer.startWidth = 0.1f;
            CameraPoseRenderer.material = new Material(Shader.Find("Sprites/Default"));
            CameraPoseRenderer.widthMultiplier = 0.1f;

            var gradient = new Gradient();
            var alpha = 1.0f;
            _validGradient.SetKeys
            (
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.red, 0.0f),
                    new GradientColorKey(Color.yellow, 0.25f),
                    new GradientColorKey(Color.green, 0.5f),
                    new GradientColorKey(Color.blue, 0.75f),
                    new GradientColorKey(Color.magenta, 1.05f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(alpha, 0.0f), new GradientAlphaKey(alpha, 1.0f)
                }
            );

            _inValidGradient.SetKeys(  new GradientColorKey[]
                {
                    new GradientColorKey(Color.gray, 0.0f),
                    new GradientColorKey(Color.black, 1.05f),

                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(alpha, 0.0f), new GradientAlphaKey(alpha, 1.0f)
                });

            CameraPoseRenderer.colorGradient = _validGradient;
        }

        private void Update()
        {
            // Add a value every 15 frames to avoid overloading the renderer
            if (Time.frameCount % 15 != 0)
            {
                return;
            }

            //CameraPoseRenderer.colorGradient = _validGradient;

            CameraPoseRenderer.positionCount += 1;
            CameraPoseRenderer.SetPosition
            (
                CameraPoseRenderer.positionCount - 1,
                _camera.transform.position
            );
        }

        private void OnDestroy()
        {
            if (_parent)
            {
                Destroy(_parent);
            }
        }
    }
}
