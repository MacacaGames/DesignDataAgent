using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Sample01Data : ScriptableObject
{
    [System.Serializable]
    public struct NPCData
    {
        [SerializeField]
        public string name;

        [SerializeField]
        public Vector2 pos;
    }

    [System.Serializable]
    public struct MonsterData
    {
        [SerializeField]
        public string name;

        [SerializeField]
        public Vector2 pos;

        [SerializeField]
        public int level;
    }



    [SerializeField]
    NPCData[] npcList = new NPCData[0];

    [SerializeField]
    MonsterData[] monsterList = new MonsterData[0];






}
