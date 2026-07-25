import glob, os, sys

# 산출물은 이 스크립트 옆(Tools/)에 만든다. 프로젝트 루트에서 실행하는 것을 전제한다.
S = os.path.dirname(os.path.abspath(__file__)).replace("\\", "/")
BASE = "Library/Bee/artifacts/1900b0aE.dag"


def build(rsp_name, out_name, want_editor):
    """Unity가 마지막에 쓴 rsp에서 참조/define만 재사용하고 소스 목록은 현재 디스크 상태로 교체한다."""
    base = open(f"{BASE}/{rsp_name}", encoding="utf-8").read().splitlines()
    out = []
    for line in base:
        if line.startswith('"Assets/'):
            continue
        if line.startswith("-out:"):
            out.append(f'-out:"{S}/{out_name}.dll"')
            continue
        if line.startswith("-refout:"):
            out.append(f'-refout:"{S}/{out_name}.ref.dll"')
            continue
        # 에디터 어셈블리는 런타임 dll(보통 ref 어셈블리)을 참조한다 → 방금 만든 것으로 바꿔친다.
        # Unity가 쓴 rsp의 참조는 stale이라, 안 바꾸면 새 타입이 "없다"고 나오는 가짜 에러가 뜬다.
        if want_editor and line.startswith("-r:"):
            ref = line.rstrip('"')
            if ref.endswith("Assembly-CSharp.ref.dll"):
                out.append(f'-r:"{S}/verify-runtime.ref.dll"')
                continue
            if ref.endswith("Assembly-CSharp.dll"):
                out.append(f'-r:"{S}/verify-runtime.dll"')
                continue
        out.append(line)

    count = 0
    for path in glob.glob("Assets/**/*.cs", recursive=True):
        path = path.replace(os.sep, "/")
        is_editor = "/Editor/" in path
        if is_editor != want_editor:
            continue
        out.append(f'"{path}"')
        count += 1

    open(f"{S}/{out_name}.rsp", "w", encoding="utf-8").write("\n".join(out) + "\n")
    return count


if __name__ == "__main__":
    if sys.argv[1] == "runtime":
        print(build("Assembly-CSharp.rsp", "verify-runtime", False))
    else:
        print(build("Assembly-CSharp-Editor.rsp", "verify-editor", True))
