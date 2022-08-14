import os
import re
import math
from PIL import Image


def add_margin(pil_img, top, right, bottom, left, color):
    width, height = pil_img.size
    new_width = width + right + left
    new_height = height + top + bottom
    result = Image.new("RGBA", (new_width, new_height), color)
    result.paste(pil_img, (left, top))
    return result


def find_frame_max(_frames):
    size = [0, 0]
    for _frameId, _frameData in _frames.items():
        # print(_frameData)
        size[0] = max(size[0], _frameData["size"][0])
        size[1] = max(size[1], _frameData["size"][1])
    return size


# im_new = add_margin(im, 50, 10, 0, 100, (128, 0, 64))
fileRegEx = r"^(\d+)\_(\d+)\-(\d+).(?:png|PNG)$"
workDirectory = os.getcwd()
workDirectoryContents = os.scandir()

archives = {}

for entry in workDirectoryContents:
    if entry.is_dir():
        # print("Processing ", entry.path)
        directoryContents = os.scandir(entry.path)
        for obj in directoryContents:
            if obj.is_file():
                fileName = obj.name
                searchResult = re.search(fileRegEx, fileName)
                if searchResult:
                    archiveId = searchResult.group(1)
                    if archiveId not in archives:
                        archives[archiveId] = {}

                    recordId = searchResult.group(2)
                    if recordId not in archives[archiveId]:
                        archives[archiveId][recordId] = {}

                    frame = searchResult.group(3)
                    if frame not in archives[archiveId][recordId]:
                        archives[archiveId][recordId][frame] = {}

                    filePath = obj.path
                    archives[archiveId][recordId][frame]["path"] = filePath
                    img = Image.open(filePath)
                    archives[archiveId][recordId][frame]["size"] = img.size
                    img.close()


for archiveId, records in archives.items():
    for recordId, frames in records.items():
        frameMax = find_frame_max(frames)
        for frameId, frame in frames.items():
            widthDifference = max(0, frameMax[0] - frame["size"][0])
            heightDifference = max(0, frameMax[1] - frame["size"][1])
            print("ArchiveID:", archiveId, "RecordID:", recordId, "FrameID:", frameId,
                  "Difference:", widthDifference, heightDifference)

            paddingLeft = math.floor(widthDifference / 2)
            paddingRight = widthDifference - paddingLeft
            paddingTop = heightDifference
            paddingBottom = 0

            img = Image.open(frame["path"])
            newImg = add_margin(img, paddingTop, paddingRight, paddingBottom, paddingLeft, (0, 0, 0, 0))
            img.close()
            newImg.save(frame["path"])

print("successfully padded all images")
