﻿using Perrinn424.Utilities;
using UnityEngine;

namespace Perrinn424.UI
{
    public class LapRow : MonoBehaviour
    {
        [SerializeField]
        private TimeCell [] timeCells;

        private void Reset()
        {
            timeCells = this.GetComponentsInChildren<TimeCell>();
        }

        public void Refresh(string title, LapTime lap, FormatCell format)
        {
            timeCells[0].SetText(title);
            timeCells[0].ApplyFormat(format);
            for (int i = 0; i < lap.TimesCount; i++)
            {
                Refresh(i, lap[i], format);
            }
        }

        public void Refresh(int cellIndex, float time, FormatCell format)
        {
            timeCells[cellIndex + 1].SetTime(time);
            timeCells[cellIndex + 1].ApplyFormat(format);
        }

        public void ApplyFormat(int cellIndex, FormatCell format)
        {
            //+1 because the first one is for the title and has not format
            timeCells[cellIndex+1].ApplyFormat(format);
        }
    } 
}
