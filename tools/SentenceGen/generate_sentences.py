# 商品名（sentences.json）の自動生成分（id に _g が付くもの）を作り直す。
# 手で書いた商品名（_g の付かないもの）はそのまま残し、足りない分だけを vocab.py の語から作る。
#
# 方針（商品名データ 見直し・拡張計画）
#   ・組み合わせは vocab.py に書いた「組み合わせてよい語」だけ（種類ごとの許可リスト）
#   ・難易度ごとに読みの文字数の目安を守る（初級2～8・中級6～13・上級10～20・達人18～35）
#   ・中級は「種類＋特徴・産地・量のどれか一つ」、上級は「二つまで」
#   ・達人は「英字＋名前＋～副題～」を半分まで、残りは長い説明形の商品名
#   ・量や個数だけが違う名前は、各難易度で2割まで。同じ名前の量違いは1つまで
#   ・同じ種類や同じ語ばかりにならないよう、なるべく均等に使う
#   ・言い換えだけの名前（大きな卵と大玉卵など）は同じ難易度に並べない。ミルクと牛乳は別の名前として扱う
#
# 使い方：python3 tools/SentenceGen/generate_sentences.py（同じ内容なら毎回同じ結果になる）
import collections
import json
import math
import os
import random
import re
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vocab import V  # noqa: E402

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
P = os.path.join(ROOT, 'Assets', 'Resources', 'Data', 'sentences.json')
BAND = {1: (2, 8), 2: (6, 13), 3: (10, 20), 4: (18, 35)}
TARGET = {1: 25, 2: 35, 3: 40, 4: 33}
QTY_SHARE = 0.2

raw = open(P, encoding='utf-8-sig').read()
bom = raw.startswith('﻿')
data = json.loads(raw.lstrip('﻿'))
HAND = [s for s in data['sentences'] if '_g' not in s['id']]
used_text = {s['text'] for s in HAND}


# 言い換え（同じ意味の語）。同じ難易度に言い換えだけの名前を並べない（違う難易度なら置いてよい）。
# ミルクと牛乳は別の名前として扱う。
SYN = (
    ('大きな', '大玉', '大きい'),
    ('粉末', '粉'),
    ('台所用', '食器用', 'キッチン用'),
    ('衣料用', '洗濯用', '洗濯'),
    ('ボックス', '箱'),
    ('詰め替え', 'つめかえ'),
    ('朝採れ', '朝採り', '朝どれ'),
    ('朝しぼり', '搾りたて'),
    ('瓶入り', '瓶'),
    ('一口', 'ひとくち'),
    ('卵', 'たまご', '玉子'),
)


SYN_TO = {w: f'<{i}>' for i, g in enumerate(SYN) for w in g}
SYN_RE = re.compile('|'.join(sorted(map(re.escape, SYN_TO), key=len, reverse=True)))


def plain(t):
    """「の」「産」の有無だけが違う名前を同じものとみなす（北海道産大豆＝北海道大豆、地鶏の卵＝地鶏卵）"""
    for a, b in (('の', ''), ('産', ''), (' ', '')):
        t = t.replace(a, b)
    return t


def norm(t):
    """言い換えで同じになる名前を同じものとみなす（大きな卵＝大玉卵 など）"""
    return SYN_RE.sub(lambda m: SYN_TO[m.group(0)], plain(t))


# 読みが同じ名前と「の」「産」の有無だけの違いは、難易度が違っても作らない。言い換えは同じ商品・同じ難易度の中だけ避ける
used_read = {s['reading'] for s in HAND}
used_plain = {plain(s['text']) for s in HAND}
used_norm = {(s['itemIds'][0], s['level'], norm(s['text'])) for s in HAND}


def overlap(*texts):
    """2つの部品に同じ語（ひらがなだけではない2文字）が入っていれば True（例：宇治抹茶アイス ～宇治の…～）"""
    def grams(t):
        return {t[i:i + 2] for i in range(len(t) - 1)
                if not all('\u3041' <= ch <= '\u309f' for ch in t[i:i + 2]) and ' ' not in t[i:i + 2]}
    seen = set()
    for t in texts:
        g = grams(t)
        if seen & g:
            return True
        seen |= g
    return False


def double_origin(*texts):
    """産地が二つ入る（宮崎県産国産豚もも など）"""
    return sum(1 for t in texts if '産' in t) >= 2


def cond_ok(cond, body, kind):
    """副題・英字の条件。k=使ってよい種類, n=名前に入っていてほしい語, x=入っていてはいけない語（; で並べる）"""
    for c in filter(None, cond.split(';')):
        key, vals = c.split('=')
        vals = vals.split(',')
        if key == 'k' and kind not in vals:
            return False
        if key == 'n' and not any(v in body for v in vals):
            return False
        if key == 'x' and any(v in body for v in vals):
            return False
    return True


PLACES = ('北海道', '山', '牧場', '農場', 'やんばる', '棚田', '高原', '産', '畑', '宇治', 'イタリア', '十勝', '温泉', '全国', '雪室', 'はさ掛け')


def fits(level, reading):
    a, b = BAND[level]
    return a <= len(reading) <= b


def C(parts, kind, tags, qty=None):
    """parts: (表記, よみ) の並び。tags: 均等に使いたい語。qty: 量の語（量違いの判定に使う）"""
    texts = [p[0] for p in parts]
    if len(parts) > 1 and (overlap(*texts) or double_origin(*texts)):
        return None
    t = ''.join(p[0] for p in parts)
    r = ''.join(p[1] for p in parts)
    base = ''.join(p[0] for p in parts if p is not qty) if qty else t
    return dict(t=t, r=r, kind=kind, tags=tags, qty=qty is not None, base=base)


def one_attr(k):
    out = [C([(k['t'], k['r'])], k['t'], [])]
    for m in k['mods']:
        out.append(C([m, (k['t'], k['r'])], k['t'], ['m:' + m[0]]))
    for o in k['org']:
        out.append(C([o, (k['t'], k['r'])], k['t'], ['o:' + o[0]]))
    for u in k['use']:
        out.append(C([(k['t'], k['r']), u], k['t'], ['u:' + u[0]]))
    for q in k['qty']:
        out.append(C([(k['t'], k['r']), q], k['t'], ['q:' + q[0]], qty=q))
    return [c for c in out if c]


def two_attr(k):
    kt = (k['t'], k['r'])
    out = []
    for o in k['org']:
        for m in k['mods']:
            out.append(C([o, m, kt], k['t'], ['o:' + o[0], 'm:' + m[0]]))
        for u in k['use']:
            out.append(C([o, kt, u], k['t'], ['o:' + o[0], 'u:' + u[0]]))
        for q in k['qty']:
            out.append(C([o, kt, q], k['t'], ['o:' + o[0], 'q:' + q[0]], qty=q))
    for m in k['mods']:
        for u in (k['use'] if k['mu'] else []):
            out.append(C([m, kt, u], k['t'], ['m:' + m[0], 'u:' + u[0]]))
        for q in k['qty']:
            out.append(C([m, kt, q], k['t'], ['m:' + m[0], 'q:' + q[0]], qty=q))
    for u in k['use']:
        for q in k['qty']:
            if u[0].endswith('入り') and q[0].endswith('入り'):
                continue
            out.append(C([kt, u, q], k['t'], ['u:' + u[0], 'q:' + q[0]], qty=q))
    return [c for c in out if c]


def pick(rng, item, pool, n, level, kind_cap, tag_cap, qty_bases, extra_ok=None, max_relax=2):
    """候補から n 個選ぶ。種類と語の使用回数の上限を少しずつ緩めながら選び、なるべく均等にする。"""
    pool = [c for c in pool if fits(level, c['r'])]
    rng.shuffle(pool)
    chosen = []
    kinds = collections.Counter()
    tags = collections.Counter()
    qty_max = math.floor(n * QTY_SHARE)
    qn = 0
    for relax in range(0, max_relax + 1):
        for c in pool:
            if len(chosen) >= n:
                return chosen
            if c['t'] in used_text or c['r'] in used_read or plain(c['t']) in used_plain or (item, level, norm(c['t'])) in used_norm:
                continue
            if kinds[c['kind']] >= kind_cap + relax:
                continue
            if any(tags[g] >= tag_cap + relax for g in c['tags']):
                continue
            if not c['qty'] and c['t'] in qty_bases:
                continue
            if c['qty']:
                if qn >= qty_max or c['base'] in qty_bases:
                    continue
                if any(t != c['t'] and t.startswith(c['base']) for t in used_text):
                    continue
            if extra_ok and not extra_ok(c):
                continue
            chosen.append(c)
            used_text.add(c['t'])
            used_read.add(c['r'])
            used_plain.add(plain(c['t']))
            used_norm.add((item, level, norm(c['t'])))
            kinds[c['kind']] += 1
            for g in c['tags']:
                tags[g] += 1
            if c['qty']:
                qn += 1
                qty_bases.add(c['base'])
    return chosen


def main():
    rng = random.Random(20261127)
    new = []
    report = []
    for item, v in V.items():
        hand = collections.Counter(s['level'] for s in HAND if s['itemIds'][0] == item)
        kinds = v['kinds']
        qty_bases = set()
        got = {}

        # 初級：手で選んだ短い名前と、種類そのものの名前（短いもの）
        l1pool = [C([w], w[0], []) for w in v['l1']] + [C([(k['t'], k['r'])], k['t'], []) for k in kinds]
        need1 = max(0, TARGET[1] - hand[1])
        got[1] = pick(rng, item, l1pool, need1, 1, 99, 99, qty_bases, max_relax=0)
        short = need1 - len(got[1])
        target = dict(TARGET)
        target[3] += short // 2
        target[4] += short - short // 2

        # 中級：種類＋一つ
        p2 = [c for k in kinds for c in one_attr(k)]
        need = max(0, target[2] - hand[2])
        got[2] = pick(rng, item, p2, need, 2, math.ceil(need / len(kinds)) + 1, 2, qty_bases)

        # 上級：種類＋二つまで（一つで長いものも可）
        p3 = [c for k in kinds for c in two_attr(k)] + p2
        need = max(0, target[3] - hand[3])
        got[3] = pick(rng, item, p3, need, 3, math.ceil(need / len(kinds)) + 1, 3, qty_bases)

        # 達人：説明形（残り）と英字＋副題（半分まで）
        need = max(0, target[4] - hand[4])
        # 説明形を先に選び（目安は残りの半分強）、英字＋副題で残りを埋める
        n_long = need - (need - hand[4]) // 2 + hand[4] // 2
        kmap = {k['t']: k for k in kinds}
        plong = []
        for ph in v['long']:
            for kn in ph['kinds']:
                k = kmap[kn]
                kt = (k['t'], k['r'])
                pt = (ph['t'], ph['r'])
                # 説明形には特徴の語を重ねない（意味の重なりや食い違いを避ける）。産地は説明に場所がないときだけ
                plong.append(C([pt, kt], kn, ['p:' + ph['t']]))
                if not any(w in ph['t'] for w in PLACES):
                    for o in k['org']:
                        plong.append(C([pt, o, kt], kn, ['p:' + ph['t'], 'o:' + o[0]]))
        plong = [c for c in plong if c]
        got_long = pick(rng, item, plong, n_long, 4, 3, 3, qty_bases, max_relax=0)
        pen = []
        bodies = [c for c in p2 if not c['qty']]
        for e in v['en']:
            for b in bodies:
                if not cond_ok(e[1], b['t'], b['kind']):
                    continue
                for s in v['sub']:
                    if not cond_ok(s[2] if len(s) > 2 else '', b['t'], b['kind']) or overlap(b['t'], s[0]):
                        continue
                    pen.append(dict(t=f"{e[0]} {b['t']} {s[0]}", r=e[0].lower() + b['r'] + s[1], kind=b['kind'],
                                    tags=['e:' + e[0], 's:' + s[0]] + b['tags'], qty=False, base='', body=b['t']))
        # 英字＋副題は達人の半分まで（手書きの達人4件もこの形なので数に入れる）
        n_en = max(0, min(need - len(got_long), len(got_long) - hand[4]))
        bodies_used = set()

        def body_once(c):
            if c['body'] in bodies_used:
                return False
            bodies_used.add(c['body'])
            return True
        got_en = pick(rng, item, pen, n_en, 4, 2, 2, qty_bases, extra_ok=body_once, max_relax=1)
        got[4] = got_long + got_en

        for lv in (1, 2, 3, 4):
            for c in got[lv]:
                new.append((item, lv, c['t'], c['r']))
        report.append((item, [hand[lv] + len(got[lv]) for lv in (1, 2, 3, 4)], len(got_long), len(got_en)))

    cnt = collections.Counter()
    S = list(HAND)
    for item, lv, t, r in new:
        cnt[item] += 1
        S.append({"id": f"s_{item}_g{cnt[item]:03d}", "itemIds": [item], "text": t, "reading": r, "level": lv})
    data['sentences'] = S
    open(P, 'w', encoding='utf-8').write(('﻿' if bom else '') + json.dumps(data, ensure_ascii=False, indent=2) + '\n')

    tot = collections.Counter()
    for item, c, nl, ne in report:
        print(f'{item:13s} 初級{c[0]:3d} 中級{c[1]:3d} 上級{c[2]:3d} 達人{c[3]:3d}（説明形{nl}・英字{ne}）')
        for i in range(4):
            tot[i] += c[i]
    print('合計', len(S), '（初級', tot[0], '中級', tot[1], '上級', tot[2], '達人', tot[3], '）')


main()
