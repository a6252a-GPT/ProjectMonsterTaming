using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.CommanderSkill
{
    public sealed class SkillInscriptionGraphic : MaskableGraphic
    {
        public float Progress { get; set; }
        public float Clock { get; set; }
        public int Tier { get; set; }
        public void Redraw() => SetVerticesDirty();
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .44f;
            float p = Mathf.Clamp01(Progress);
            float dissolve = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.70f, 1f, p));
            Color ink = color; ink.a *= dissolve;
            float turn = Clock * .25f;
            Ring(vh, r, 2.5f, turn, 96, ink);
            Ring(vh, r * .83f, 1.5f, -turn, 64, ink * new Color(1,1,1,.6f));
            for (int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6+turn;
                Line(vh, Point(a)*r*.89f, Point(a)*r*.98f, 2, ink);
                if(i%2==0) Line(vh, Point(a)*r*.68f, Point(a+Mathf.PI*2/3)*r*.68f, 1.6f, ink);
            }
            // 달과 별이 봉인 중심을 이룬다.
            for(int i=0;i<32;i++)
            {
                float a=Mathf.Lerp(.6f,5.68f,i/32f), b=Mathf.Lerp(.6f,5.68f,(i+1)/32f);
                Line(vh,Point(a)*r*.30f,Point(b)*r*.30f,2f+5f*Mathf.Sin(i*Mathf.PI/32f),ink);
            }
            Star(vh,new Vector2(r*.20f,r*.15f),r*.12f,ink);
            for(int i=0;i<18;i++)
            {
                float a=i*2.39996f+Clock*(.25f+Tier*.12f);
                float travel=Mathf.Clamp01(p*1.5f);
                float distance=Mathf.Lerp(r*(1.05f+(i%3)*.15f),r*.08f,travel);
                var pos=Point(a)*distance;
                var c=ink; c.a *= .45f+.55f*Mathf.Sin(i+Clock*3)*Mathf.Sin(i+Clock*3);
                Star(vh,pos,Mathf.Lerp(2f,4f,p)+(i%3),c);
            }
            if(p>.65f)
            {
                float burst=(p-.65f)/.35f; var c=color; c.a=(1-burst)*.8f;
                Ring(vh,r*(.25f+burst*.95f),2*(1-burst)+.3f,0,Tier>=3?6:64,c);
                for(int i=0;i<12;i++) Star(vh,Point(i*Mathf.PI/6)*r*burst,4*(1-burst),c);
            }
        }
        static Vector2 Point(float a)=>new Vector2(Mathf.Cos(a),Mathf.Sin(a));
        static void Ring(VertexHelper vh,float r,float w,float start,int n,Color c)
        { for(int i=0;i<n;i++) Line(vh,Point(start+i*Mathf.PI*2/n)*r,Point(start+(i+1)*Mathf.PI*2/n)*r,w,c); }
        static void Star(VertexHelper vh,Vector2 p,float r,Color c)
        { Line(vh,p-Vector2.up*r,p+Vector2.up*r,Mathf.Max(.4f,r*.35f),c); Line(vh,p-Vector2.right*r*.65f,p+Vector2.right*r*.65f,Mathf.Max(.4f,r*.35f),c); }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float w,Color c)
        {
            Vector2 d=(b-a).normalized; var n=new Vector2(-d.y,d.x)*w*.5f; int k=vh.currentVertCount;
            vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);
            vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
        }
    }
}
