"""Kontrola: texty „overit“ z novinky.json sa nachádzajú v stiahnutých oficiálnych stránkach."""
import json,re,html,sys,os
# použitie: python over.py novinky.json <priečinok so stiahnutými stránkami (vetva zdroje)>
S=(sys.argv[2] if len(sys.argv) > 2 else '.').rstrip('/') + '/'
mapa={
 "https://www.minedu.sk/ai-gramotnost-prichadza-na-gymnazia-ministerstvo-meni-statny-vzdelavaci-program/":"z2/novinky3/minedu_ai.html",
 "https://www.statpedu.sk/sk/svp/inovovany-statny-vzdelavaci-program/inovovany-svp-gymnazia-so-stvorrocnym-patrocnym-vzdelavacim-programom/":"z2/novinky3/statpedu_gym.html",
 "https://nivam.sk/pre-skoly/maturitne-skusky/":"z2/novinky3/nivam_maturitne.html",
 "https://www2.nucem.sk/sk/merania/narodne-merania/maturita/o-maturite":"z2/novinky2/nucem_o_maturite.html",
 "https://www2.nucem.sk/sk/merania/narodne-merania/maturita":"novinky1/nucem_maturita.html",
 "https://www2.nucem.sk/sk/merania/narodne-merania/maturita/roky/2026-2027":"z2/novinky3/nucem_ms_rok.html",
 "https://www.minedu.sk/pre-skolsky-rok-20272028/":"z2/novinky3/minedu_pre_2027_2028.html",
 "https://eprihlasky.iedu.sk/":"z2/novinky2/eprihlasky.html",
 "https://www.minedu.sk/44151-sk/stredne-skoly/":"z2/novinky3/minedu_ss_faq.html",
 "https://www.minedu.sk/testovanie-9-v-skolskom-roku-2026-2027/":"z2/novinky3/minedu_t9_2026_2027.html",
}
def norm(t):
    t=html.unescape(t).lower()
    t=re.sub(r'[‐-―−]','-',t)
    return re.sub(r'[\s ­​]+','',t)
def text(c):
    s=open(c,'rb').read().decode('utf-8','replace')
    s=re.sub(r'(?is)<(script|style)\b.*?</\1>',' ',s)
    s=re.sub(r'<[^>]+>',' ',s)
    return norm(s)
d=json.load(open(sys.argv[1],encoding='utf-8'))
zle=0
for p in d['polozky']:
    for z in p['zdroje']:
        for t in z.get('overit',[]):
            f=mapa.get(z['url'])
            if not f: print('BEZ KOPIE',z['url']); continue
            if not os.path.exists(S+f): print("CHÝBA KÓPIA",f); continue
            ok=norm(t) in text(S+f)
            if not ok: zle+=1; print('NENÁJDENÉ',p['id'],'|',t,'|',f)
print('chýb',zle)
