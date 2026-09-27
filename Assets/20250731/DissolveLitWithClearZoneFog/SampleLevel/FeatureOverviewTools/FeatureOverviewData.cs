using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace LiangZhu.Demo
{
    // Demo-only data. All sampling, layout and export code is editor-only.
    public sealed class FeatureOverviewData : MonoBehaviour
    {
        public GameObject[] stages;
        public Camera[] cameras;
        public DissolveController[] controllers;
        public PlayableDirector director, multiDirector;
        public Camera boardCamera;
        public Canvas boardCanvas;
        public RawImage boardImage;
        public Texture2D sharedNoise;
        public Texture2D[] previews = new Texture2D[17];
        public float[] noiseAmounts = { 0, .35f, .7f };
        public float[] planeOffsets = { .65f, 1.45f };
        public float radialRadius = 1.05f;
        public float localAmount = .5f, worldOffset = 1.1f;
        public float previewTime = 1.2f, multiPreviewTime = 3;
        public bool proceduralNoise, labels = true;
        public DissolveController.DissolveSpace noiseSpace = DissolveController.DissolveSpace.Local;
        public float[] timelineAmounts = new float[4];
        public Vector3[] multiValues = new Vector3[4];
    }
}
