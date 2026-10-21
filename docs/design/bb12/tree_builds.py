"""Enumerate BB-12 candidate tree builds; design tool, not game code.

Run: python docs/design/bb12/tree_builds.py
The cost/gate rules match bb12-skill-trees-candidate.md.
"""

from itertools import product


def builds_at(points: int):
    if not 1 <= points <= 10:
        raise ValueError("ball level/point budget must be 1..10")

    for vitality, power, growth in product(range(3), repeat=3):
        tier1 = vitality + power + growth
        for skill in ("L1", "A2", "B2", "A3", "B3"):
            skill2 = skill != "L1"
            skill3 = skill.endswith("3")
            for start, keystone in product((False, True), ("", "A", "B")):
                tier2 = int(skill2) + (2 if start else 0)
                if tier2 and tier1 < 2:
                    continue
                if (skill3 or keystone) and tier1 + tier2 < 5:
                    continue
                spent = tier1 + tier2 + int(skill3) + (3 if keystone else 0)
                if spent == points:
                    yield (vitality, power, growth, skill, start, keystone)


def main():
    for points in (1, 4, 7, 10):
        print(f"LV{points}: {sum(1 for _ in builds_at(points))} exact-spend builds")


if __name__ == "__main__":
    main()
