<?xml version="1.0" encoding="UTF-8"?>
<Playlist xmlns="http://www.dvdforum.org/2005/HDDVDVideo/Playlist" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://www.dvdforum.org/2005/HDDVDVideo/Playlist Playlist.xsd" majorVersion="1" minorVersion="0" description="test">
	<Configuration>
		<StreamingBuffer size="0"/>
		<Aperture size="1920x1080"/>
		<MainVideoDefaultColor color="108080"/>
	</Configuration>

	<TitleSet timeBase="60fps">
		<FirstPlayTitle titleDuration="00:00:03:01" >
			<PrimaryAudioVideoClip titleTimeBegin="00:00:00:00" src="file:///dvddisc/HVDVD_TS/PEVOB1.MAP" titleTimeEnd="00:00:03:01" dataSource="Disc">
				<Video track="1"/>
			</PrimaryAudioVideoClip>

		</FirstPlayTitle>

		<Title id="MM" titleNumber="1" displayName="MM" titleDuration="00:01:00:01" onEnd="MM">
			<PrimaryAudioVideoClip titleTimeBegin="00:00:00:00" src="file:///dvddisc/HVDVD_TS/PEVOB2.MAP" titleTimeEnd="00:01:00:01" dataSource="Disc">
				<Video track="1"/>
				<Audio track="1" streamNumber="1"/>
			</PrimaryAudioVideoClip>
			<ApplicationSegment zOrder="0" titleTimeBegin="00:00:03:00" titleTimeEnd="00:01:00:01" src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/NASA.xmf" sync="soft">
				<ApplicationResource src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/NASA.xmf" priority="1" multiplexed="false" size="822" description="archive" loadingBegin="00:00:01:00"/>
				<ApplicationResource src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/script.js" priority="1" multiplexed="false" size="3852" description="archive" loadingBegin="00:00:01:00"/>
				<ApplicationResource loadingBegin="00:00:00:00" priority="1" size="6993" src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/NASA.xmu" multiplexed="false" />
				<ApplicationResource loadingBegin="00:00:00:00" priority="1" size="4288" src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/nasa_mm_8.png" multiplexed="false" />
				<!--<ApplicationResource loadingBegin="00:00:00:00" priority="1" size="1480" src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/nasa_mm_8_a.png" multiplexed="false" />
				<ApplicationResource loadingBegin="00:00:00:00" priority="1" size="1252" src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/nasa_mm_8_0.png" multiplexed="false" />-->
			</ApplicationSegment>
			
									
			<ChapterList>
				<Chapter titleTimeBegin="00:00:00:00"/>
			</ChapterList>

			<TrackNavigationList>
				<VideoTrack track="1"/>

				<AudioTrack track="1" langcode="en:00"/>
			</TrackNavigationList>

		</Title>
		<Title id="feature" titleNumber="2" displayName="feature" titleDuration="00:58:06:00" onEnd="card">
			<PrimaryAudioVideoClip titleTimeBegin="00:00:00:00" src="file:///dvddisc/HVDVD_TS/PEVOB3.MAP" titleTimeEnd="00:58:06:00" dataSource="Disc">
				<Video track="1"/>
				<Audio track="1" streamNumber="1"/>
				<Audio track="2" streamNumber="2"/>
				<Audio track="3" streamNumber="3"/>
				<Audio track="4" streamNumber="4"/>
				<Audio track="5" streamNumber="5"/>
				<Audio track="6" streamNumber="6"/>
				<Audio track="7" streamNumber="7"/>
				<Audio track="8" streamNumber="8"/>
			</PrimaryAudioVideoClip>
			<ApplicationSegment zOrder="0" titleTimeBegin="00:00:03:00" titleTimeEnd="00:58:06:00" src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/feature.xmf" sync="soft">
				<ApplicationResource src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/feature.xmf" priority="1" multiplexed="false" size="561" description="archive" loadingBegin="00:00:01:00"/>
				<ApplicationResource src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/feature.js" priority="1" multiplexed="false" size="1572" description="archive" loadingBegin="00:00:01:00"/>
				<ApplicationResource loadingBegin="00:00:00:00" priority="1" size="1059" src="file:///dvddisc/ADV_OBJ/ApplicationTest.aca/feature.xmu" multiplexed="false" />
			</ApplicationSegment>
			<ChapterList>
				<Chapter titleTimeBegin="00:00:00:00" displayName="Chapter 1" description="Chapter 1"/>
				<Chapter titleTimeBegin="00:04:42:00" displayName="Chapter 2" description="Chapter 2"/>
				<Chapter titleTimeBegin="00:09:17:00" displayName="Chapter 3" description="Chapter 3"/>
				<Chapter titleTimeBegin="00:12:55:00" displayName="Chapter 4" description="Chapter 4"/>
				<Chapter titleTimeBegin="00:16:36:00" displayName="Chapter 5" description="Chapter 5"/>
				<Chapter titleTimeBegin="00:19:59:00" displayName="Chapter 6" description="Chapter 6"/>
				<Chapter titleTimeBegin="00:21:42:00" displayName="Chapter 7" description="Chapter 7"/>
				<Chapter titleTimeBegin="00:24:33:00" displayName="Chapter 8" description="Chapter 8"/>
				<Chapter titleTimeBegin="00:28:25:00" displayName="Chapter 9" description="Chapter 9"/>
				<Chapter titleTimeBegin="00:32:19:00" displayName="Chapter 10" description="Chapter 10"/>
				<Chapter titleTimeBegin="00:37:43:00" displayName="Chapter 11" description="Chapter 11"/>
				<Chapter titleTimeBegin="00:40:56:00" displayName="Chapter 12" description="Chapter 12"/>
				<Chapter titleTimeBegin="00:43:05:00" displayName="Chapter 13" description="Chapter 13"/>
				<Chapter titleTimeBegin="00:47:35:00" displayName="Chapter 14" description="Chapter 14"/>
				<Chapter titleTimeBegin="00:50:28:00" displayName="Chapter 15" description="Chapter 15"/>
				<Chapter titleTimeBegin="00:53:46:00" displayName="Chapter 16" description="Chapter 16"/>
			</ChapterList>
			
			<TrackNavigationList>
				<VideoTrack track="1"/>
				
				<AudioTrack track="1" langcode="en:00"/>
				<AudioTrack track="2" langcode="en:00"/>
				<AudioTrack track="3" langcode="en:00"/>
				<AudioTrack track="4" langcode="en:00"/>
				<AudioTrack track="5" langcode="en:00"/>
				<AudioTrack track="6" langcode="en:00"/>
				<AudioTrack track="7" langcode="en:00"/>
				<AudioTrack track="8" langcode="en:00"/>
			</TrackNavigationList>
		</Title>	
		<Title id="card" titleNumber="3" displayName="card" titleDuration="00:00:10:00" onEnd="logo">
				<PrimaryAudioVideoClip titleTimeBegin="00:00:00:00" src="file:///dvddisc/HVDVD_TS/PEVOB4.MAP" titleTimeEnd="00:00:10:00" dataSource="Disc">
					<Video track="1"/>
				</PrimaryAudioVideoClip>
				
				<ChapterList>
					<Chapter titleTimeBegin="00:00:00:00"/>
				</ChapterList>
				
				<TrackNavigationList>
					<VideoTrack track="1"/>
					
				</TrackNavigationList>
				
			</Title>
			<Title id="logo" titleNumber="4" displayName="logo" titleDuration="00:00:20:00" onEnd="MM">
				<PrimaryAudioVideoClip titleTimeBegin="00:00:00:00" src="file:///dvddisc/HVDVD_TS/PEVOB5.MAP" titleTimeEnd="00:00:20:00" dataSource="Disc">
					<Video track="1"/>
				</PrimaryAudioVideoClip>
				
				<ChapterList>
					<Chapter titleTimeBegin="00:00:00:00"/>
				</ChapterList>
				
				<TrackNavigationList>
					<VideoTrack track="1"/>
					
				</TrackNavigationList>
			</Title>
	</TitleSet>
</Playlist>
