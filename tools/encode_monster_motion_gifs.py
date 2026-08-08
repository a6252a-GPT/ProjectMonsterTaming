from pathlib import Path
from PIL import Image
import shutil
import sys


def encode_all(frame_root: Path, output_root: Path, fps: int = 8) -> int:
    output_root.mkdir(parents=True, exist_ok=True)
    encoded = 0

    for frame_directory in sorted(path for path in frame_root.iterdir() if path.is_dir()):
        frame_paths = sorted(frame_directory.glob("frame_*.png"))
        if not frame_paths:
            continue

        frames = [Image.open(path).convert("P", palette=Image.Palette.ADAPTIVE, colors=256) for path in frame_paths]
        output_path = output_root / f"{frame_directory.name}.gif"
        frames[0].save(
            output_path,
            save_all=True,
            append_images=frames[1:],
            duration=max(1, round(1000 / fps)),
            loop=0,
            disposal=2,
            optimize=True,
        )

        for frame in frames:
            frame.close()
        shutil.rmtree(frame_directory)
        encoded += 1
        if encoded % 25 == 0:
            print(f"[GIF] {encoded}개 인코딩 완료", flush=True)

    return encoded


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("사용법: encode_monster_motion_gifs.py <frame_root> <output_root>")

    count = encode_all(Path(sys.argv[1]), Path(sys.argv[2]))
    print(f"[GIF] 전체 인코딩 완료: {count}개", flush=True)
