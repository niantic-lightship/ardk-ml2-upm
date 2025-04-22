// Copyright 2022-2025 Niantic.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Samples;
using UnityEngine;
using UnityEngine.UI;

public class ExportScanPanel : MonoBehaviour
{
    [Tooltip("The slider used to show the export progress")] [SerializeField]
    private Text _exportText;

    [Tooltip("Used to animate progress")] [SerializeField]
    private Text _progressText;

    private bool _isWorking;

    [SerializeField]
    private Text _validationText;

    private string a;
    private string b;
    private string c;

    private void Update()
    {
        if (_isWorking)
        {
            int numberOfDots = Mathf.RoundToInt(Time.time) % 3 + 1;
            string dotsText = string.Concat(Enumerable.Range(0, numberOfDots).Select(t => "."));
            a = "Exporting" + dotsText;
        }
        else
        {
            a = "Export Complete";
        }
        Output();
    }

    public void SetExportStatusText(bool isWorking, string path)
    {
        this._isWorking = isWorking;
        b = path;
        Output();
    }

    public void SetValidationText(string text)
    {
        c = text;
        Output();
    }

    private void Output()
    {
        _exportText.text = $"{a}\n{b}\n{c}";
    }
}
