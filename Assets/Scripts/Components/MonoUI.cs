using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Entities;

[GenerateAuthoringComponent]
public class MonoUI : IComponentData
{
    public TextMeshProUGUI txt;
}
