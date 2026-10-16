if(UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit Mode only");
var font=UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Font/DungGeunMo SDF.asset");
// 최초 상태는 동적 glyph/character table이 비어 있었다. 미리보기로 생성된 캐시만 제거한다.
font.ClearFontAssetData(true);
// ClearFontAssetData는 기존 ligature 캐시도 비우므로, 시작 시 있던 3개 항목은 보존한다.
var so=new UnityEditor.SerializedObject(font);
var records=so.FindProperty("m_FontFeatureTable.m_LigatureSubstitutionRecords");records.arraySize=3;
int[][] components={new[]{181,930,15131},new[]{160,15140},new[]{883,15140}};
int[] ligatures={15940,167,892};
for(int i=0;i<3;i++){
 var record=records.GetArrayElementAtIndex(i);var ids=record.FindPropertyRelative("m_ComponentGlyphIDs");ids.arraySize=components[i].Length;
 for(int j=0;j<components[i].Length;j++)ids.GetArrayElementAtIndex(j).intValue=components[i][j];
 record.FindPropertyRelative("m_LigatureGlyphID").intValue=ligatures[i];
}
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(font);UnityEditor.AssetDatabase.SaveAssetIfDirty(font);
return new{glyphs=font.glyphTable.Count,characters=font.characterTable.Count};
