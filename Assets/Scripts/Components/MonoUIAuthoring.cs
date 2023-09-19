using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Entities;

public class MonoUI : IComponentData
{
    public TextMeshProUGUI txt;
}

public class MonoUIAuthoring : MonoBehaviour
{
	public TextMeshProUGUI txt;
}