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
       
        <Title titleNumber="1" titleDuration="02:05:16:22" id="MainMovie" displayName="Main Movie" onEnd="WarningCards" alternativeSDDisplayMode="letterbox">
            <PrimaryAudioVideoClip titleTimeBegin="00:00:00:00" titleTimeEnd="01:03:48:27" src="file:///dvddisc/HVDVD_TS/FEATURE_1.MAP" dataSource="Disc">
                <Video track="1" mediaAttr="1"/>
                <Audio track="1" streamNumber="1" mediaAttr="1" description="English 5.1"/>
                <Audio track="2" streamNumber="2" mediaAttr="1" description="Francais 5.1"/>
                <Audio track="3" streamNumber="3" mediaAttr="1" description="Espanol 5.1"/>
                <Audio track="4" streamNumber="4" mediaAttr="2" description="Feature Commentary 2.0"/>
            </PrimaryAudioVideoClip>
            <PrimaryAudioVideoClip titleTimeBegin="01:03:48:27" titleTimeEnd="02:05:16:22" src="file:///dvddisc/HVDVD_TS/FEATURE_2.MAP" dataSource="Disc" seamless="true">
                <Video track="1" mediaAttr="1"/>
                <Audio track="1" streamNumber="1" mediaAttr="1" description="English 5.1"/>
                <Audio track="2" streamNumber="2" mediaAttr="1" description="Francais 5.1"/>
                <Audio track="3" streamNumber="3" mediaAttr="1" description="Espanol 5.1"/>
                <Audio track="4" streamNumber="4" mediaAttr="2" description="Feature Commentary 2.0"/>
            </PrimaryAudioVideoClip>
            <ChapterList>
    			<Chapter displayName="Count To Ten" titleTimeBegin="00:00:00:00" />
    			<Chapter displayName="Engagement Party" titleTimeBegin="00:04:02:10" />
    			<Chapter displayName="Agent Lindsey Farris" titleTimeBegin="00:12:57:05" />
    			<Chapter displayName="Chopper Chase" titleTimeBegin="00:21:16:47" />
    			<Chapter displayName="Intelligence" titleTimeBegin="00:26:30:42" />
    			<Chapter displayName="Trust and Understanding" titleTimeBegin="00:32:54:30" />
    			<Chapter displayName="The Vatican" titleTimeBegin="00:39:28:07" />
    			<Chapter displayName="Finding Davian" titleTimeBegin="00:46:53:37" />
    			<Chapter displayName="Identity Theft" titleTimeBegin="00:51:41:12" />
    			<Chapter displayName="Dangerous Cargo" titleTimeBegin="00:57:31:25" />
    			<Chapter displayName="Bridge Attack" titleTimeBegin="01:02:05:15" />
    			<Chapter displayName="The Tables Turn" titleTimeBegin="01:09:02:30" />
    			<Chapter displayName="Shanghai" titleTimeBegin="01:18:14:32" />
    			<Chapter displayName="Skyscraper" titleTimeBegin="01:23:26:22" />
    			<Chapter displayName="Night Flight" titleTimeBegin="01:27:26:15" />
    			<Chapter displayName="It's Complicated" titleTimeBegin="01:34:02:22" />
    			<Chapter displayName="Mobile Navigator" titleTimeBegin="01:42:37:17" />
    			<Chapter displayName="Point And Shoot" titleTimeBegin="01:51:04:10" />
    			<Chapter displayName="Mission: Honeymoon" titleTimeBegin="01:57:20:47" />
    		</ChapterList>

            <TrackNavigationList>
                <AudioTrack track="1" langcode="en:01" selectable="true"/>
                <AudioTrack track="2" langcode="fr:01" selectable="true"/>
                <AudioTrack track="3" langcode="es:01" selectable="true"/>
                <AudioTrack track="4" langcode="en:03" selectable="true"/>
            </TrackNavigationList>
        </Title>
    </TitleSet>
</Playlist>