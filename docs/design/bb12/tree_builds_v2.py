"""Enumerate the proposed Identity V-inspired BB-12 web in bb12-persona-web-v2.md.

This verifies costs and prerequisites only. It does not simulate combat.
Run: python docs/design/bb12/tree_builds_v2.py
"""

from itertools import product


def builds_at(points: int):
    if not 1 <= points <= 10:
        raise ValueError("ball level must be 1..10")

    for vitality, power, growth in product(range(3), repeat=3):
        tier1 = vitality + power + growth
        for start, skill, final in product((False, True), ("L1", "A2", "B2", "A3", "B3"), ("", "A", "B")):
            skill2 = skill != "L1"
            skill3 = skill.endswith("3")
            if start and (growth < 1 or tier1 < 2):
                continue
            if skill2 and (growth < 1 or tier1 < 2):
                continue
            lower_spent = tier1 + 2 * start + int(skill2)
            if skill3 and lower_spent < 5:
                continue
            if final and (growth < 1 or not skill3 or skill[0] != final):
                continue
            spent = lower_spent + int(skill3) + 3 * bool(final)
            if spent == points:
                yield vitality, power, growth, start, skill, final


if __name__ == "__main__":
    for level in (1, 4, 7, 9, 10):
        builds = list(builds_at(level))
        print(f"LV{level}: {len(builds)} exact-spend builds; {sum(bool(b[-1]) for b in builds)} with final talent")
