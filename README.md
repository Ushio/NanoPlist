## NanoPlist
NanoPlist is A reader writer for Apple Property Lists (.plist) binary and xml format for C# (C Sharp)
Binary IO is optimized less GC, fast.
 
 Developing by unity.

## Type
NanoPlist use system data type.

|Plist|Type|
|:---|:---|
|Nil|not supported|
|Number(boolean)|bool|
|Number(integer)|int|
|Number(real)|double|
|String|string|
|Date|DateTime|
|Data|byte[]|
|Array|Generic.List&lt;object&gt;|
|Dictionary|Generic.Dictionary&lt;string,object&gt;|

## Testing
data-driven testing like QuickCheck.
NanoPlistProject/Assets/NanoPlistPlayModeTest.cs

![render](test.png)

## Developing note for binary format
- integer 1, 2, 4 byte is unsigned, 8 is only signed
- dictionary is key, key, key...., value, value, value... order.
- OffsetIntSize can handle all byte for object and offsetTable without trailer. "Not Max OffsetInt Size"

## Other Library
**plist-cil**<br>
Looks good. but has unity little compatibility problem. 
And Cocoa like API have pros and cons.<br>
https://github.com/claunia/plist-cil

**PlistCS**<br>
Single file library. Simple is best. but have unicode problem, and use global variable.<br>
https://github.com/animetrics/PlistCS

## Acknowledgments
Super good document.

How to decode Apple Binary Property List files
https://synalysis.com/how-to-decode-apple-binary-property-list-files/

Property List/Binary
http://fileformats.archiveteam.org/wiki/Property_List/Binary

Apple Open Source, has any helpfull comment.
https://opensource.apple.com/source/CF/CF-550/CFBinaryPList.c

