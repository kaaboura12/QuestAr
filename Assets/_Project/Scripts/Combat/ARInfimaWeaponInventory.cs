using System.Collections.Generic;
using InfimaGames.LowPolyShooterPack;
using UnityEngine;

/// <summary>
/// Two-weapon inventory. Equip swaps which gun is active; the arms animator is updated separately.
/// </summary>
public class ARInfimaWeaponInventory : InventoryBehaviour
{
    static readonly Quaternion GripRotation = Quaternion.Euler(90f, 0f, 0f);

    readonly List<WeaponBehaviour> weapons = new List<WeaponBehaviour>();
    WeaponBehaviour equipped;
    int equippedIndex = -1;

    public int Count => weapons.Count;

    public void SetLoadout(IReadOnlyList<WeaponBehaviour> loadout, int equippedAtStart = 0)
    {
        weapons.Clear();
        if (loadout != null)
        {
            for (int i = 0; i < loadout.Count; i++)
            {
                if (loadout[i] != null && !weapons.Contains(loadout[i]))
                    weapons.Add(loadout[i]);
            }
        }

        if (weapons.Count == 0)
        {
            equipped = null;
            equippedIndex = -1;
            return;
        }

        equippedAtStart = Mathf.Clamp(equippedAtStart, 0, weapons.Count - 1);
        for (int i = 0; i < weapons.Count; i++)
        {
            ResetGrip(weapons[i]);
            PrepareWeapon(weapons[i]);
            weapons[i].gameObject.SetActive(i == equippedAtStart);
        }

        equippedIndex = equippedAtStart;
        equipped = weapons[equippedIndex];
    }

    public void Bind(WeaponBehaviour weapon)
    {
        if (weapon == null)
            return;

        if (!weapons.Contains(weapon))
            weapons.Add(weapon);

        if (equipped != null)
            return;

        equipped = weapon;
        equippedIndex = weapons.IndexOf(weapon);
        if (!equipped.gameObject.activeSelf)
            equipped.gameObject.SetActive(true);
    }

    public override void Init(int equippedAtStart = 0)
    {
        if (weapons.Count == 0)
        {
            WeaponBehaviour[] found = GetComponentsInChildren<WeaponBehaviour>(true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !weapons.Contains(found[i]))
                    weapons.Add(found[i]);
            }
        }

        if (weapons.Count == 0)
        {
            equipped = null;
            equippedIndex = -1;
            return;
        }

        if (equipped == null)
        {
            equippedIndex = Mathf.Clamp(equippedAtStart, 0, weapons.Count - 1);
            equipped = weapons[equippedIndex];
        }

        if (equipped != null && !equipped.gameObject.activeSelf)
            equipped.gameObject.SetActive(true);
    }

    public override WeaponBehaviour Equip(int index)
    {
        if (weapons.Count == 0)
            Init();

        if (index < 0 || index >= weapons.Count)
            return equipped;

        if (equippedIndex == index && equipped != null)
            return equipped;

        if (equipped != null)
        {
            ResetGrip(equipped);
            equipped.gameObject.SetActive(false);
        }

        equippedIndex = index;
        equipped = weapons[equippedIndex];
        ResetGrip(equipped);
        PrepareWeapon(equipped);
        equipped.gameObject.SetActive(true);
        return equipped;
    }

    public override WeaponBehaviour GetEquipped() => equipped;

    public override int GetEquippedIndex() => equippedIndex;

    public override int GetLastIndex()
    {
        if (weapons.Count == 0)
            return equippedIndex;

        int index = equippedIndex - 1;
        if (index < 0)
            index = weapons.Count - 1;
        return index;
    }

    public override int GetNextIndex()
    {
        if (weapons.Count == 0)
            return equippedIndex;

        int index = equippedIndex + 1;
        if (index >= weapons.Count)
            index = 0;
        return index;
    }

    static void ResetGrip(WeaponBehaviour weapon)
    {
        if (weapon == null)
            return;

        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = GripRotation;
        weapon.transform.localScale = Vector3.one;
    }

    static void PrepareWeapon(WeaponBehaviour weapon)
    {
        if (weapon == null)
            return;

        Animator animator = weapon.GetComponent<Animator>();
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }
}
