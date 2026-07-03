<?xml version="1.0" encoding="UTF-8"?>
<Playlist majorVersion="1" minorVersion="0" type="Advanced" xmlns="http://www.dvdforum.org/2005/HDDVDVideo/Playlist">
	<Configuration>
		<StreamingBuffer size="0"></StreamingBuffer>
		<Aperture size="1920x1080"></Aperture>
		<MainVideoDefaultColor color="108080"></MainVideoDefaultColor>
	</Configuration>
	<MediaAttributeList>
		<VideoAttributeItem index="1" codec="VC-1"></VideoAttributeItem>
		<AudioAttributeItem index="1" codec="DTS-HD"></AudioAttributeItem>
		<AudioAttributeItem index="2" codec="LPCM"></AudioAttributeItem>
	</MediaAttributeList>
	<TitleSet timeBase="60fps" defaultLanguage="fr">
		<Title id="init" titleNumber="1" titleDuration="00:00:08:00" onEnd="warning" displayName="init" description="init">
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/black.MAP" titleTimeBegin="00:00:00:00" titleTimeEnd="00:00:08:00" clipTimeBegin="00:00:00:00" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
			</PrimaryAudioVideoClip>
			<ScheduledControlList>
				<PauseAt titleTime="00:00:00:30"></PauseAt>
			</ScheduledControlList>
			<TrackNavigationList>
				<VideoTrack track="1" selectable="true" ></VideoTrack>
			</TrackNavigationList>
		</Title>
		<Title id="warning" titleNumber="2" titleDuration="00:00:08:00" onEnd="warning" displayName="warning" description="warning">
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/black.MAP" titleTimeBegin="00:00:00:00" titleTimeEnd="00:00:08:00" clipTimeBegin="00:00:00:00" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
			</PrimaryAudioVideoClip>
			<ScheduledControlList>
				<PauseAt titleTime="00:00:00:30"></PauseAt>
			</ScheduledControlList>
			<TrackNavigationList>
				<VideoTrack track="1" selectable="true"></VideoTrack>
			</TrackNavigationList>
		</Title>
		<Title id="mainMenuLoop" titleNumber="3" titleDuration="00:02:01:42" onEnd="mainMenuLoop" displayName="mainMenuLoop" description="mainMenuLoop">
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/introMenu.MAP" titleTimeBegin="00:00:00:00" titleTimeEnd="00:00:31:42" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
				<Audio track="1" streamNumber="1" mediaAttr="1"></Audio>
			</PrimaryAudioVideoClip>
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/mainMenu.MAP" titleTimeBegin="00:00:31:42" titleTimeEnd="00:01:01:42" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
				<Audio track="1" streamNumber="1" mediaAttr="1"></Audio>
			</PrimaryAudioVideoClip>
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/mainMenu.MAP" titleTimeBegin="00:01:01:42" titleTimeEnd="00:01:31:42" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
				<Audio track="1" streamNumber="1" mediaAttr="1"></Audio>
			</PrimaryAudioVideoClip>
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/mainMenu.MAP" titleTimeBegin="00:01:31:42" titleTimeEnd="00:02:01:42" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
				<Audio track="1" streamNumber="1" mediaAttr="1"></Audio>
			</PrimaryAudioVideoClip>
			<ScheduledControlList>
				<Event id="mainMenuEnd" titleTime="00:02:01:38"></Event>
				<PauseAt titleTime="00:02:01:40"></PauseAt>
			</ScheduledControlList>
			<TrackNavigationList>
				<VideoTrack track="1" selectable="true"></VideoTrack>
				<AudioTrack track="1" selectable="true" langcode="fr:00"></AudioTrack>
			</TrackNavigationList>
		</Title>
		<Title id="mainMovie" titleNumber="4" titleDuration="02:30:12:00" onEnd="warning" displayName="mainMovie" description="mainMovie">
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/L0_mainMovie.MAP" titleTimeBegin="00:00:00:00" titleTimeEnd="01:21:26:30" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
				<Audio track="1" streamNumber="1" mediaAttr="1"></Audio>
			</PrimaryAudioVideoClip>
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/L1_mainMovie.MAP" titleTimeBegin="01:21:26:30" titleTimeEnd="02:30:10:00" seamless="true">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
				<Audio track="1" streamNumber="1" mediaAttr="1"></Audio>
			</PrimaryAudioVideoClip>
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/black.MAP" titleTimeBegin="02:30:10:00" titleTimeEnd="02:30:12:00" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
			</PrimaryAudioVideoClip>
			<ApplicationSegment titleTimeBegin="00:00:00:00" titleTimeEnd="02:30:10:00" src="file:///dvddisc/ADV_OBJ/infomenu.aca/infomenu.xmf" sync="soft" zOrder="0" autorun="true"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:00:27:25" titleTimeEnd="00:07:21:09" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0000.xmf" sync="soft" group="2" zOrder="1"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:07:17:18" titleTimeEnd="00:10:07:43" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0035.xmf" sync="soft" group="2" zOrder="2"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:10:10:03" titleTimeEnd="00:13:49:06" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0070.xmf" sync="soft" group="2" zOrder="3"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:13:45:49" titleTimeEnd="00:15:56:18" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0105.xmf" sync="soft" group="2" zOrder="4"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:15:52:25" titleTimeEnd="00:18:52:19" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0140.xmf" sync="soft" group="2" zOrder="5"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:18:48:27" titleTimeEnd="00:21:08:57" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0175.xmf" sync="soft" group="2" zOrder="6"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:21:05:07" titleTimeEnd="00:27:32:55" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0210.xmf" sync="soft" group="2" zOrder="7"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:27:29:06" titleTimeEnd="00:31:28:48" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0245.xmf" sync="soft" group="2" zOrder="8"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:31:26:13" titleTimeEnd="00:36:17:27" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0280.xmf" sync="soft" group="2" zOrder="9"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:36:13:42" titleTimeEnd="00:38:45:57" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0315.xmf" sync="soft" group="2" zOrder="10"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:38:42:07" titleTimeEnd="00:43:00:57" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0350.xmf" sync="soft" group="2" zOrder="11"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:42:57:43" titleTimeEnd="00:48:39:13" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0385.xmf" sync="soft" group="2" zOrder="12"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:48:39:57" titleTimeEnd="00:51:38:51" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0420.xmf" sync="soft" group="2" zOrder="13"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:51:35:06" titleTimeEnd="00:53:56:24" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0455.xmf" sync="soft" group="2" zOrder="14"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:53:53:49" titleTimeEnd="00:59:15:57" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0490.xmf" sync="soft" group="2" zOrder="15"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:59:12:55" titleTimeEnd="01:04:24:21" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0525.xmf" sync="soft" group="2" zOrder="16"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:04:20:31" titleTimeEnd="01:09:55:54" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0560.xmf" sync="soft" group="2" zOrder="17"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:09:55:07" titleTimeEnd="01:12:31:48" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0595.xmf" sync="soft" group="2" zOrder="18"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:12:28:21" titleTimeEnd="01:18:06:03" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0630.xmf" sync="soft" group="2" zOrder="19"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:18:02:55" titleTimeEnd="01:20:49:27" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0665.xmf" sync="soft" group="2" zOrder="20"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:20:46:39" titleTimeEnd="01:27:18:27" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0700.xmf" sync="soft" group="2" zOrder="21"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:27:16:19" titleTimeEnd="01:32:10:36" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0735.xmf" sync="soft" group="2" zOrder="22"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:32:09:00" titleTimeEnd="01:50:22:07" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0770.xmf" sync="soft" group="2" zOrder="23"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:50:19:00" titleTimeEnd="01:53:42:30" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0805.xmf" sync="soft" group="2" zOrder="24"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:53:39:19" titleTimeEnd="01:56:31:00" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0840.xmf" sync="soft" group="2" zOrder="25"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="01:56:31:18" titleTimeEnd="02:00:33:06" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0875.xmf" sync="soft" group="2" zOrder="26"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="02:00:34:06" titleTimeEnd="02:03:53:01" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0910.xmf" sync="soft" group="2" zOrder="27"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="02:03:49:12" titleTimeEnd="02:10:02:39" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0945.xmf" sync="soft" group="2" zOrder="28"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="02:09:59:24" titleTimeEnd="02:19:35:01" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-0980.xmf" sync="soft" group="2" zOrder="29"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="02:19:31:18" titleTimeEnd="02:29:49:48" src="file:///dvddisc/ADV_OBJ/suben.aca/subsen-1015.xmf" sync="soft" group="2" zOrder="30"></ApplicationSegment>
			<ApplicationSegment titleTimeBegin="00:00:00:00" titleTimeEnd="02:30:10:00" src="file:///dvddisc/ADV_OBJ/suben.aca/advsubs.xmf" group="2" zOrder="31">
				<ApplicationResource multiplexed="false" priority="1" src="file:///dvddisc/ADV_OBJ/suben.aca" size="7036057"></ApplicationResource>
			</ApplicationSegment>
			<AdvancedSubtitleSegment titleTimeBegin="00:00:00:00" titleTimeEnd="02:29:52:48" src="file:///dvddisc/ADV_OBJ/mainApp.aca/advsubs.xmf">
				<Subtitle track="1"></Subtitle>
			</AdvancedSubtitleSegment>
			<ChapterList>
				<Chapter id="mainMovie_ch1" titleTimeBegin="00:00:00:00"></Chapter>
				<Chapter id="mainMovie_ch2" titleTimeBegin="00:03:25:55"></Chapter>
				<Chapter id="mainMovie_ch3" titleTimeBegin="00:07:53:42"></Chapter>
				<Chapter id="mainMovie_ch4" titleTimeBegin="00:14:01:27"></Chapter>
				<Chapter id="mainMovie_ch5" titleTimeBegin="00:22:57:07"></Chapter>
				<Chapter id="mainMovie_ch6" titleTimeBegin="00:32:04:25"></Chapter>
				<Chapter id="mainMovie_ch7" titleTimeBegin="00:39:21:12"></Chapter>
				<Chapter id="mainMovie_ch8" titleTimeBegin="00:44:33:45"></Chapter>
				<Chapter id="mainMovie_ch9" titleTimeBegin="00:54:16:12"></Chapter>
				<Chapter id="mainMovie_ch10" titleTimeBegin="01:03:24:27"></Chapter>
				<Chapter id="mainMovie_ch11" titleTimeBegin="01:14:29:55"></Chapter>
				<Chapter id="mainMovie_ch12" titleTimeBegin="01:20:56:07"></Chapter>
				<Chapter id="mainMovie_ch13" titleTimeBegin="01:26:46:07"></Chapter>
				<Chapter id="mainMovie_ch14" titleTimeBegin="01:37:01:10"></Chapter>
				<Chapter id="mainMovie_ch15" titleTimeBegin="01:43:09:07"></Chapter>
				<Chapter id="mainMovie_ch16" titleTimeBegin="01:49:40:12"></Chapter>
				<Chapter id="mainMovie_ch17" titleTimeBegin="01:57:58:07"></Chapter>
				<Chapter id="mainMovie_ch18" titleTimeBegin="02:03:44:32"></Chapter>
				<Chapter id="mainMovie_ch19" titleTimeBegin="02:11:49:20"></Chapter>
				<Chapter id="mainMovie_ch20" titleTimeBegin="02:16:36:47"></Chapter>
			</ChapterList>
			<TrackNavigationList>
				<VideoTrack track="1" selectable="true" ></VideoTrack>
				<AudioTrack track="1" selectable="true"  langcode="fr:00"></AudioTrack>
				<SubtitleTrack track="1" langcode="en:00" selectable="true" ></SubtitleTrack>
			</TrackNavigationList>
		</Title>
		<Title id="scLogo" titleNumber="5" titleDuration="00:00:19:00" onEnd="mainMovie" displayName="scLogo" description="scLogo">
			<PrimaryAudioVideoClip src="file:///dvddisc/HVDVD_TS/scLogo_movie.MAP" titleTimeBegin="00:00:00:00" titleTimeEnd="00:00:19:00" clipTimeBegin="00:00:00:00" seamless="false">
				<Video track="1" angleNumber="1" mediaAttr="1"></Video>
				<Audio track="1" streamNumber="1" mediaAttr="1"></Audio>
			</PrimaryAudioVideoClip>
			<TrackNavigationList>
				<VideoTrack track="1" selectable="true"></VideoTrack>
				<AudioTrack track="1" selectable="true" langcode="fr:00"></AudioTrack>
			</TrackNavigationList>
		</Title>
	</TitleSet>
</Playlist>