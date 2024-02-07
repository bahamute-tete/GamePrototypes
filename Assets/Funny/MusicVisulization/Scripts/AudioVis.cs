using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class AudioVis : MonoBehaviour
{
    AudioSource _audioSource;
    public static float[] _samplesLeft = new float[512];
    public static float[] _samplesRight = new float[512];

    public static float[] _samples = new float[512];
    public static float[] _freqBand = new float[8];
    public static float[] _bandBuffer = new float[8];
    float[] _bufferDecrease = new float[8];

    public float[] _freqBandHighest = new float[8];
    public static float[] _audioBand = new float[8];
    public static float[] _audioBandBuffer = new float[8];
    public float[] visBandBuffer = new float[8];


    public static float _amplitude, _amplitudeBuffer;
    float _ampitudeHighest;

    public float _audioProfile = 0;

    public enum _channel {Stereo,Left,Right };
    public _channel channel = new _channel();

    public AudioClip _audioClip;
    public bool _useMicphone;
    public string _selectedDevice;
    public AudioMixerGroup _mixerGroupMaster;
    public AudioMixerGroup _mixerGroupMicphone;

    // Start is called before the first frame update
    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        AudioProfile(_audioProfile);

        if (_useMicphone)
        {
            if (Microphone.devices.Length > 0)
            {
                _selectedDevice = Microphone.devices[0];
                _audioSource.outputAudioMixerGroup = _mixerGroupMicphone;
                _audioSource.clip = Microphone.Start(_selectedDevice, true, 300, AudioSettings.outputSampleRate);
                if (Microphone.IsRecording(_selectedDevice))
                {
                    Debug.Log("IsRecording");
                }
            }
            else
            {
                _audioSource.outputAudioMixerGroup = _mixerGroupMaster;
                _useMicphone = false;
            }
        }

        if (!_useMicphone)
        {
            _audioSource.clip = _audioClip;
        }

        _audioSource.Play();
        
    }

    // Update is called once per frame
    void Update()
    {
        if (_useMicphone)
            _audioSource.timeSamples = Microphone.GetPosition(_selectedDevice);

        GetSpectumAudioSource();
        MakeFrequencyBands();
        BandBuffer();
        CreateAudioBands();
        GetAmplitude();
        GetSpectrumAudioSource();

    }

    void GetSpectrumAudioSource()
    {
        _audioSource.GetSpectrumData(_samplesLeft, 0, FFTWindow.Blackman);
        _audioSource.GetSpectrumData(_samplesRight, 1, FFTWindow.Blackman);
    }

    void AudioProfile(float _audioProfile)
    {

        for (int i = 0; i < 8; i++)
        {
            _freqBandHighest[i] = _audioProfile;
        }
    }
    void GetAmplitude()
    {
        float _CurrentAmp = 0;
        float _CurrentAmpBuffer = 0;
        for (int i = 0; i < 8; i++)
        {
            _CurrentAmp += _audioBand[i];
            _CurrentAmpBuffer += _audioBandBuffer[i];
        }

        if (_CurrentAmp > _ampitudeHighest)
        { 
            _ampitudeHighest = _CurrentAmp;
        }

        _amplitude = _CurrentAmp / (_ampitudeHighest+Mathf.Epsilon);
        _amplitudeBuffer = _CurrentAmpBuffer / (_ampitudeHighest + Mathf.Epsilon);

       // Debug.Log("_ampitudeHighest " + _ampitudeHighest);
    }

    void CreateAudioBands()
    {
        for (int i = 0; i < 8; i++)
        {
            if (_freqBand[i] > _freqBandHighest[i])
            {
                _freqBandHighest[i] = _freqBand[i];
            }

       
            _audioBand[i] = _freqBand[i] / (_freqBandHighest[i]+Mathf.Epsilon);
            _audioBandBuffer[i] = _bandBuffer[i]/ (_freqBandHighest[i] + Mathf.Epsilon);
            visBandBuffer[i] = _bandBuffer[i] / (_freqBandHighest[i]);

            //Debug.Log("_freqBand[" + i + "] = " + _freqBand[i]);
            // Debug.Log("_freqBandHighest[" + i + "] = " + _freqBandHighest[i]);
        }




    }    
    void GetSpectumAudioSource()
    {
        _audioSource.GetSpectrumData(_samples, 0, FFTWindow.Blackman);
    }

    void BandBuffer()
    {
        for (int i = 0; i < 8; i++)
        {
            if (_freqBand[i] >= _bandBuffer[i])
            {
                _bandBuffer[i] = _freqBand[i];
                _bufferDecrease[i] = 0.005f;
            }
            else
            {
                _bandBuffer[i] -= _bufferDecrease[i];
                _bufferDecrease[i] *= 1.2f;
            }
        }
    }

    void MakeFrequencyBands()
    {
        /*
         * 22050/512 =43
         *20-60Hz
         *250-500Hz
         *500-2000Hz
         *2000-4000Hz
         *4000-6000Hz
         *6000-20000Hz
         *
         *0 - 2 =86Hz
         *1 - 4 =142Hz  87-258
         *2 - 8 =344Hz  259-602
         *3 - 16=688Hz  603-1290
         *4 - 32 =1376Hz 1291-2666
         *5 - 64=2752Hz  2667-5418 
         *6 - 128=5504Hz 5419-10922
         *7 - 256=11008Hz 10923-21930
         *510
         */

        int count = 0;
        for (int i = 0; i < 8; i++)
        {
            float average = 0;
            int sampleCount = (int)Mathf.Pow(2, i)*2;
            if (i == 7)
            {
                sampleCount += 2;
            }
            for (int j = 0; j < sampleCount; j++)
            {
                if (channel == _channel.Stereo)
                {
                    //average += _samples[count] * (sampleCount + 1);
                    average += (_samplesLeft[count] + _samplesRight[count]);

                }

                if (channel == _channel.Left)
                {
                    average += (_samplesLeft[count]);
                }

                if (channel ==_channel.Right)
                {
                    average += (_samplesRight[count]);
                }
                count++;
            }
            average /= sampleCount;
            _freqBand[i] = average * 10;
            //Debug.Log("sampleCount[" + i + "] = " + sampleCount);
        }
    }
}
