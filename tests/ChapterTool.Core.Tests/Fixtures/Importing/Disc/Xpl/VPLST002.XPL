<?xml version="1.0" encoding="UTF-8"?>
<!-- 
   Deluxe Digital Studios
   Paramount Studios 
   Reds 1
   08/2006
   Bahareh Navai
-->
<Playlist majorVersion="1" minorVersion="0"
    xmlns="http://www.dvdforum.org/2005/HDDVDVideo/Playlist"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:schemaLocation="http://www.dvdforum.org/2005/HDDVDVideo/Playlist http://www.dvdforum.org/2005/HDDVDVideo/Playlist/Playlist.xsd">
    <Configuration>
        <StreamingBuffer size="0"/>
        <Aperture size="1920x1080"/>
        <MainVideoDefaultColor color="107F7F"/>
    </Configuration>
    <MediaAttributeList>
        <VideoAttributeItem index="1" codec="VC-1"/>
        <VideoAttributeItem index="2" codec="AVC"/>
        <VideoAttributeItem index="3" codec="MPEG-2"/>
        <AudioAttributeItem index="1" codec="DD+"/>
        <AudioAttributeItem index="2" codec="AC-3"/>
        <SubpictureAttributeItem index="1" codec="8bitRLC"/>
    </MediaAttributeList>
    <TitleSet timeBase="60fps" tickBase="24fps" defaultLanguage="en">
        <Title titleNumber="1" titleDuration="01:43:53:24" id="MainMovie" displayName="Main Movie" onEnd="NEXT" alternativeSDDisplayMode="letterbox">
            <PrimaryAudioVideoClip titleTimeBegin="00:00:00:00" titleTimeEnd="01:03:21:10" src="file:///dvddisc/HVDVD_TS/FEATURE_1.MAP" dataSource="Disc">
                <Video track="1" mediaAttr="1"/>
                <Audio track="1" streamNumber="1" mediaAttr="1" description="English 5.1"/>
                <Subtitle track="1" streamNumber="1" mediaAttr="1" description="English"/>
            </PrimaryAudioVideoClip>
            <PrimaryAudioVideoClip titleTimeBegin="01:03:21:10" titleTimeEnd="01:43:53:24" src="file:///dvddisc/HVDVD_TS/FEATURE_2.MAP" dataSource="Disc" seamless="true">
                <Video track="1" mediaAttr="1"/>
                <Audio track="1" streamNumber="1" mediaAttr="1" description="English 5.1"/>
              
                <Subtitle track="1" streamNumber="1" mediaAttr="1" description="English"/>
            </PrimaryAudioVideoClip>
            <ChapterList>
                <Chapter displayName="Chapter  1" titleTimeBegin="00:00:00:00" />
                <Chapter displayName="Chapter  2" titleTimeBegin="00:05:24:57" />
                <Chapter displayName="Chapter  3" titleTimeBegin="00:18:27:07" />
                <Chapter displayName="Chapter  4" titleTimeBegin="00:30:36:30" />
                <Chapter displayName="Chapter  5" titleTimeBegin="00:39:46:05" />
                <Chapter displayName="Chapter  6" titleTimeBegin="01:03:21:10" />
                <Chapter displayName="Chapter  7" titleTimeBegin="01:09:49:30" />
                <Chapter displayName="Chapter  8" titleTimeBegin="01:19:54:30" />
                <Chapter displayName="Chapter  9" titleTimeBegin="01:29:31:45" />
                <Chapter displayName="Chapter 10" titleTimeBegin="01:36:01:12" />
            </ChapterList>
            <TrackNavigationList>
                <AudioTrack track="1" langcode="en:01" selectable="true"/>
                <SubtitleTrack track="1" langcode="en:01" selectable="true"/>
            </TrackNavigationList>
        </Title>
    </TitleSet>
</Playlist>